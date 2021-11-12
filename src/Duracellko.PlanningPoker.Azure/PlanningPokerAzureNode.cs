﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Duracellko.PlanningPoker.Azure.Configuration;
using Duracellko.PlanningPoker.Azure.ServiceBus;
using Duracellko.PlanningPoker.Domain;
using Duracellko.PlanningPoker.Domain.Serialization;
using Microsoft.Extensions.Logging;

namespace Duracellko.PlanningPoker.Azure
{
    /// <summary>
    /// Instance of Planning Poker application in Windows Azure. Synchronizes the planning poker teams with other nodes.
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:ElementsMustAppearInTheCorrectOrder", Justification = "Destructor is placed together with Dispose.")]
    public class PlanningPokerAzureNode : IDisposable
    {
        private const string DeletedTeamPrefix = "Deleted:";

        private readonly InitializationList _teamsToInitialize = new InitializationList();
        private readonly ScrumTeamSerializer _scrumTeamSerializer;
        private readonly ILogger<PlanningPokerAzureNode> _logger;

        private IDisposable _sendNodeMessageSubscription;
        private IDisposable _serviceBusScrumTeamMessageSubscription;
        private IDisposable _serviceBusTeamCreatedMessageSubscription;
        private IDisposable _serviceBusRequestTeamListMessageSubscription;
        private IDisposable _serviceBusRequestTeamsMessageSubscription;
        private IDisposable _serviceBusTeamListMessageSubscription;
        private IDisposable _serviceBusInitializeTeamMessageSubscription;

        private volatile string _processingScrumTeamName;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlanningPokerAzureNode"/> class.
        /// </summary>
        /// <param name="planningPoker">The planning poker teams controller instance.</param>
        /// <param name="serviceBus">The service bus used to send messages between nodes.</param>
        /// <param name="configuration">The configuration of planning poker for Azure platform.</param>
        /// <param name="scrumTeamSerializer">The serializer that provides serialization and desserialization of Scrum Team.</param>
        /// <param name="logger">Logger instance to log events.</param>
        public PlanningPokerAzureNode(
            IAzurePlanningPoker planningPoker,
            IServiceBus serviceBus,
            IAzurePlanningPokerConfiguration configuration,
            ScrumTeamSerializer scrumTeamSerializer,
            ILogger<PlanningPokerAzureNode> logger)
        {
            PlanningPoker = planningPoker ?? throw new ArgumentNullException(nameof(planningPoker));
            ServiceBus = serviceBus ?? throw new ArgumentNullException(nameof(serviceBus));
            Configuration = configuration ?? new AzurePlanningPokerConfiguration();
            _scrumTeamSerializer = scrumTeamSerializer ??
                new ScrumTeamSerializer(PlanningPoker.DateTimeProvider, PlanningPoker.GuidProvider, DeckProvider.Default);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            NodeId = PlanningPoker.GuidProvider.NewGuid().ToString();
        }

        /// <summary>
        /// Gets a controller of planning poker teams.
        /// </summary>
        /// <value>The planning poker controller.</value>
        public IAzurePlanningPoker PlanningPoker { get; private set; }

        /// <summary>
        /// Gets an ID of the Planning Poker node.
        /// </summary>
        public string NodeId { get; private set; }

        /// <summary>
        /// Gets a configuration of planning poker for Azure platform.
        /// </summary>
        public IAzurePlanningPokerConfiguration Configuration { get; private set; }

        /// <summary>
        /// Gets an Azure service bus object used to send messages between nodes.
        /// </summary>
        protected IServiceBus ServiceBus { get; private set; }

        /// <summary>
        /// Starts synchronization with other nodes.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task Start()
        {
            _logger.PlanningPokerAzureNodeStarting(NodeId);

            await ServiceBus.Register(NodeId);
            SetupPlanningPokerListeners();
            SetupServiceBusListeners();

            RequestTeamList();
        }

        /// <summary>
        /// Stops synchronization with other nodes.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task Stop()
        {
            _logger.PlanningPokerAzureNodeStopping(NodeId);

            if (_sendNodeMessageSubscription != null)
            {
                _sendNodeMessageSubscription.Dispose();
                _sendNodeMessageSubscription = null;
            }

            if (_serviceBusScrumTeamMessageSubscription != null)
            {
                _serviceBusScrumTeamMessageSubscription.Dispose();
                _serviceBusScrumTeamMessageSubscription = null;
            }

            if (_serviceBusTeamCreatedMessageSubscription != null)
            {
                _serviceBusTeamCreatedMessageSubscription.Dispose();
                _serviceBusTeamCreatedMessageSubscription = null;
            }

            if (_serviceBusRequestTeamListMessageSubscription != null)
            {
                _serviceBusRequestTeamListMessageSubscription.Dispose();
                _serviceBusRequestTeamListMessageSubscription = null;
            }

            if (_serviceBusRequestTeamsMessageSubscription != null)
            {
                _serviceBusRequestTeamsMessageSubscription.Dispose();
                _serviceBusRequestTeamsMessageSubscription = null;
            }

            if (_serviceBusTeamListMessageSubscription != null)
            {
                _serviceBusTeamListMessageSubscription.Dispose();
                _serviceBusTeamListMessageSubscription = null;
            }

            if (_serviceBusInitializeTeamMessageSubscription != null)
            {
                _serviceBusInitializeTeamMessageSubscription.Dispose();
                _serviceBusInitializeTeamMessageSubscription = null;
            }

            return ServiceBus.Unregister();
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing"><c>True</c> if disposing not using GC; otherwise <c>false</c>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop().Wait();
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PlanningPokerAzureNode"/> class.
        /// </summary>
        ~PlanningPokerAzureNode()
        {
            Dispose(false);
        }

        private void SetupPlanningPokerListeners()
        {
            var teamMessages = PlanningPoker.ObservableMessages.Where(m => !string.Equals(m.TeamName, _processingScrumTeamName, StringComparison.OrdinalIgnoreCase));
            var nodeTeamMessages = teamMessages
                .Where(m => m.MessageType != MessageType.Empty && m.MessageType != MessageType.TeamCreated && m.MessageType != MessageType.EstimationEnded)
                .Select(m => new NodeMessage(NodeMessageType.ScrumTeamMessage) { Data = m });
            var createTeamMessages = teamMessages.Where(m => m.MessageType == MessageType.TeamCreated)
                .Select(m => CreateTeamCreatedMessage(m.TeamName))
                .Where(m => m != null);
            var nodeMessages = nodeTeamMessages.Merge(createTeamMessages);

            _sendNodeMessageSubscription = nodeMessages.Subscribe(SendNodeMessage);
        }

        private void SetupServiceBusListeners()
        {
            var serviceBusMessages = ServiceBus.ObservableMessages.Where(m => !string.Equals(m.SenderNodeId, NodeId, StringComparison.OrdinalIgnoreCase));

            var busTeamMessages = serviceBusMessages.Where(m => m.MessageType == NodeMessageType.ScrumTeamMessage);
            _serviceBusScrumTeamMessageSubscription = busTeamMessages.Subscribe(ProcessTeamMessage);

            var busTeamCreatedMessages = serviceBusMessages.Where(m => m.MessageType == NodeMessageType.TeamCreated);
            _serviceBusTeamCreatedMessageSubscription = busTeamCreatedMessages.Subscribe(OnScrumTeamCreated);
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception would stop observable.")]
        private NodeMessage CreateTeamCreatedMessage(string teamName)
        {
            try
            {
                using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
                {
                    teamLock.Lock();
                    var team = teamLock.Team;
                    return new NodeMessage(NodeMessageType.TeamCreated)
                    {
                        Data = SerializeScrumTeam(team)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorCreateTeamNodeMessage(ex, NodeId, teamName);
                return null;
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception would stop observable.")]
        private async void SendNodeMessage(NodeMessage message)
        {
            try
            {
                message.SenderNodeId = NodeId;
                await ServiceBus.SendMessage(message);

                _logger.NodeMessageSent(NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType);
            }
            catch (Exception ex)
            {
                _logger.ErrorSendingNodeMessage(ex, NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType);
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception would stop observable.")]
        private void OnScrumTeamCreated(NodeMessage message)
        {
            try
            {
                var scrumTeam = DeserializeScrumTeam((string)message.Data);
                _logger.ScrumTeamCreatedNodeMessageReceived(NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType, scrumTeam.Name);

                if (!_teamsToInitialize.ContainsOrNotInit(scrumTeam.Name))
                {
                    try
                    {
                        _processingScrumTeamName = scrumTeam.Name;
                        using (var teamLock = PlanningPoker.AttachScrumTeam(scrumTeam))
                        {
                        }
                    }
                    finally
                    {
                        _processingScrumTeamName = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorScrumTeamCreatedNodeMessage(ex, NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType);
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception would stop observable.")]
        private void ProcessTeamMessage(NodeMessage nodeMessage)
        {
            var message = (ScrumTeamMessage)nodeMessage.Data;
            _logger.ScrumTeamNodeMessageReceived(NodeId, nodeMessage.SenderNodeId, nodeMessage.RecipientNodeId, nodeMessage.MessageType, message.TeamName, message.MessageType);
            try
            {
                if (!_teamsToInitialize.ContainsOrNotInit(message.TeamName))
                {
                    switch (message.MessageType)
                    {
                        case MessageType.MemberJoined:
                            OnMemberJoinedMessage(message.TeamName, (ScrumTeamMemberMessage)message);
                            break;
                        case MessageType.MemberDisconnected:
                            OnMemberDisconnectedMessage(message.TeamName, (ScrumTeamMemberMessage)message);
                            break;
                        case MessageType.EstimationStarted:
                            OnEstimationStartedMessage(message.TeamName);
                            break;
                        case MessageType.EstimationCanceled:
                            OnEstimationCanceledMessage(message.TeamName);
                            break;
                        case MessageType.MemberEstimated:
                            OnMemberEstimatedMessage(message.TeamName, (ScrumTeamMemberEstimationMessage)message);
                            break;
                        case MessageType.MemberActivity:
                            OnMemberActivityMessage(message.TeamName, (ScrumTeamMemberMessage)message);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorProcessingScrumTeamNodeMessage(ex, NodeId, nodeMessage.SenderNodeId, nodeMessage.RecipientNodeId, nodeMessage.MessageType, message.TeamName, message.MessageType);
            }
        }

        private void OnMemberJoinedMessage(string teamName, ScrumTeamMemberMessage message)
        {
            using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
            {
                teamLock.Lock();
                var team = teamLock.Team;
                try
                {
                    _processingScrumTeamName = team.Name;
                    var isObserver = string.Equals(message.MemberType, typeof(Observer).Name, StringComparison.OrdinalIgnoreCase);
                    var observer = team.Join(message.MemberName, isObserver);
                    observer.SessionId = message.SessionId;
                }
                finally
                {
                    _processingScrumTeamName = null;
                }
            }
        }

        private void OnMemberDisconnectedMessage(string teamName, ScrumTeamMemberMessage message)
        {
            using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
            {
                teamLock.Lock();
                var team = teamLock.Team;
                try
                {
                    _processingScrumTeamName = team.Name;
                    team.Disconnect(message.MemberName);
                }
                finally
                {
                    _processingScrumTeamName = null;
                }
            }
        }

        private void OnEstimationStartedMessage(string teamName)
        {
            using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
            {
                teamLock.Lock();
                var team = teamLock.Team;
                try
                {
                    _processingScrumTeamName = team.Name;
                    team.ScrumMaster.StartEstimation();
                }
                finally
                {
                    _processingScrumTeamName = null;
                }
            }
        }

        private void OnEstimationCanceledMessage(string teamName)
        {
            using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
            {
                teamLock.Lock();
                var team = teamLock.Team;
                try
                {
                    _processingScrumTeamName = team.Name;
                    team.ScrumMaster.CancelEstimation();
                }
                finally
                {
                    _processingScrumTeamName = null;
                }
            }
        }

        private void OnMemberEstimatedMessage(string teamName, ScrumTeamMemberEstimationMessage message)
        {
            using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
            {
                teamLock.Lock();
                var team = teamLock.Team;
                try
                {
                    _processingScrumTeamName = team.Name;
                    var member = team.FindMemberOrObserver(message.MemberName) as Member;
                    if (member != null)
                    {
                        member.Estimation = new Estimation(message.Estimation);
                    }
                }
                finally
                {
                    _processingScrumTeamName = null;
                }
            }
        }

        private void OnMemberActivityMessage(string teamName, ScrumTeamMemberMessage message)
        {
            using (var teamLock = PlanningPoker.GetScrumTeam(teamName))
            {
                teamLock.Lock();
                var team = teamLock.Team;
                try
                {
                    _processingScrumTeamName = team.Name;
                    var observer = team.FindMemberOrObserver(message.MemberName);
                    if (observer != null)
                    {
                        observer.SessionId = message.SessionId;
                        observer.AcknowledgeMessages(message.SessionId, message.AcknowledgedMessageId);
                        observer.UpdateActivity();
                    }
                }
                finally
                {
                    _processingScrumTeamName = null;
                }
            }
        }

        private void RequestTeamList()
        {
            if (!_teamsToInitialize.IsEmpty)
            {
                var serviceBusMessages = ServiceBus.ObservableMessages.Where(m => !string.Equals(m.SenderNodeId, NodeId, StringComparison.OrdinalIgnoreCase));
                var teamListActions = serviceBusMessages.Where(m => m.MessageType == NodeMessageType.TeamList).Take(1)
                    .Timeout(Configuration.InitializationMessageTimeout, Observable.Return<NodeMessage>(null))
                    .Select(m => new Action(() => ProcessTeamListMessage(m)));
                _serviceBusTeamListMessageSubscription = teamListActions.Subscribe(a => a());

                SendNodeMessage(new NodeMessage(NodeMessageType.RequestTeamList));
            }
            else
            {
                EndInitialization();
            }
        }

        private void ProcessTeamListMessage(NodeMessage message)
        {
            if (_serviceBusTeamListMessageSubscription != null)
            {
                _serviceBusTeamListMessageSubscription.Dispose();
                _serviceBusTeamListMessageSubscription = null;
            }

            if (message != null)
            {
                _logger.NodeMessageReceived(NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType);

                var teamList = (IEnumerable<string>)message.Data;
                if (_teamsToInitialize.Setup(teamList))
                {
                    PlanningPoker.SetTeamsInitializingList(teamList);
                }

                RequestTeams(message.SenderNodeId);
            }
            else
            {
                EndInitialization();
            }
        }

        private void RequestTeams(string recipientId)
        {
            if (_teamsToInitialize.IsEmpty)
            {
                EndInitialization();
            }
            else
            {
                var lockObject = new object();
                var serviceBusMessages = ServiceBus.ObservableMessages.Where(m => !string.Equals(m.SenderNodeId, NodeId, StringComparison.OrdinalIgnoreCase));
                serviceBusMessages = serviceBusMessages.Synchronize(lockObject);

                var lastMessageTime = PlanningPoker.DateTimeProvider.UtcNow;

                var initTeamActions = serviceBusMessages.Where(m => m.MessageType == NodeMessageType.InitializeTeam)
                    .TakeWhile(m => !_teamsToInitialize.IsEmpty)
                    .Select(m => new Action(() =>
                    {
                        lastMessageTime = PlanningPoker.DateTimeProvider.UtcNow;
                        ProcessInitializeTeamMessage(m);
                    }));
                var messageTimeoutActions = Observable.Interval(TimeSpan.FromSeconds(1.0)).Synchronize(lockObject)
                    .SelectMany(i => lastMessageTime + Configuration.InitializationMessageTimeout < PlanningPoker.DateTimeProvider.UtcNow ? Observable.Throw<Action>(new TimeoutException()) : Observable.Empty<Action>());

                void RetryRequestTeamList(Exception ex)
                {
                    if (!_teamsToInitialize.IsEmpty)
                    {
                        _logger.RetryRequestTeamList(NodeId);
                        RequestTeamList();
                    }
                }

                _serviceBusInitializeTeamMessageSubscription = initTeamActions.Merge(messageTimeoutActions)
                    .Subscribe(a => a(), RetryRequestTeamList);

                var requestTeamsMessage = new NodeMessage(NodeMessageType.RequestTeams)
                {
                    RecipientNodeId = recipientId,
                    Data = _teamsToInitialize.Values.ToArray()
                };
                SendNodeMessage(requestTeamsMessage);
            }
        }

        private void ProcessInitializeTeamMessage(NodeMessage message)
        {
            var scrumTeamData = (string)message.Data;
            if (scrumTeamData.StartsWith(DeletedTeamPrefix, StringComparison.Ordinal))
            {
                // team does not exist anymore
                var teamName = scrumTeamData.Substring(DeletedTeamPrefix.Length);
                _teamsToInitialize.Remove(teamName);
            }
            else
            {
                var scrumTeam = DeserializeScrumTeam(scrumTeamData);
                _logger.ScrumTeamCreatedNodeMessageReceived(NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType, scrumTeam.Name);

                _teamsToInitialize.Remove(scrumTeam.Name);
                PlanningPoker.InitializeScrumTeam(scrumTeam);
            }

            if (_teamsToInitialize.IsEmpty)
            {
                EndInitialization();
            }
        }

        private void EndInitialization()
        {
            _teamsToInitialize.Clear();

            if (_serviceBusInitializeTeamMessageSubscription != null)
            {
                _serviceBusInitializeTeamMessageSubscription.Dispose();
                _serviceBusInitializeTeamMessageSubscription = null;
            }

            PlanningPoker.EndInitialization();
            _logger.PlanningPokerAzureNodeInitialized(NodeId);

            var serviceBusMessages = ServiceBus.ObservableMessages.Where(m => !string.Equals(m.SenderNodeId, NodeId, StringComparison.OrdinalIgnoreCase));

            var requestTeamListMessages = serviceBusMessages.Where(m => m.MessageType == NodeMessageType.RequestTeamList);
            _serviceBusRequestTeamListMessageSubscription = requestTeamListMessages.Subscribe(ProcessRequestTeamListMesage);

            var requestTeamsMessages = serviceBusMessages.Where(m => m.MessageType == NodeMessageType.RequestTeams);
            _serviceBusRequestTeamsMessageSubscription = requestTeamsMessages.Subscribe(ProcessRequestTeamsMessage);
        }

        private void ProcessRequestTeamListMesage(NodeMessage message)
        {
            _logger.NodeMessageReceived(NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType);

            var scrumTeamNames = PlanningPoker.ScrumTeamNames.ToArray();
            var teamListMessage = new NodeMessage(NodeMessageType.TeamList)
            {
                RecipientNodeId = message.SenderNodeId,
                Data = scrumTeamNames
            };
            SendNodeMessage(teamListMessage);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Node will request teams again on failure.")]
        private void ProcessRequestTeamsMessage(NodeMessage message)
        {
            _logger.NodeMessageReceived(NodeId, message.SenderNodeId, message.RecipientNodeId, message.MessageType);

            var scrumTeamNames = (IEnumerable<string>)message.Data;
            foreach (var scrumTeamName in scrumTeamNames)
            {
                try
                {
                    string scrumTeamData = null;
                    try
                    {
                        using (var teamLock = PlanningPoker.GetScrumTeam(scrumTeamName))
                        {
                            teamLock.Lock();
                            scrumTeamData = SerializeScrumTeam(teamLock.Team);
                        }
                    }
                    catch (Exception)
                    {
                        scrumTeamData = null;
                    }

                    var initializeTeamMessage = new NodeMessage(NodeMessageType.InitializeTeam)
                    {
                        RecipientNodeId = message.SenderNodeId,
                        Data = scrumTeamData != null ? scrumTeamData : (DeletedTeamPrefix + scrumTeamName)
                    };
                    SendNodeMessage(initializeTeamMessage);
                }
                catch (Exception)
                {
                }
            }
        }

        private string SerializeScrumTeam(ScrumTeam scrumTeam)
        {
            var result = new StringBuilder();
            using (var writer = new StringWriter(result))
            {
                _scrumTeamSerializer.Serialize(writer, scrumTeam);
            }

            return result.ToString();
        }

        private ScrumTeam DeserializeScrumTeam(string json)
        {
            using (var reader = new StringReader(json))
            {
                return _scrumTeamSerializer.Deserialize(reader);
            }
        }
    }
}
