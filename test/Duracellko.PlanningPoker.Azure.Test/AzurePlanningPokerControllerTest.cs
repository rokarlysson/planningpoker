﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Duracellko.PlanningPoker.Azure.Configuration;
using Duracellko.PlanningPoker.Data;
using Duracellko.PlanningPoker.Domain;
using Duracellko.PlanningPoker.Domain.Test;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Duracellko.PlanningPoker.Azure.Test
{
    [TestClass]
    [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Mock objects do not need to be disposed.")]
    public class AzurePlanningPokerControllerTest
    {
        [TestMethod]
        public void ObservableMessages_TeamCreated_ScrumTeamCreatedMessage()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            target.CreateScrumTeam("test", "master", Deck.Standard);
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.TeamCreated, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
        }

        [TestMethod]
        public void ObservableMessages_MemberJoined_ScrumTeamMemberMessage()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var target = CreateAzurePlanningPokerController(guidProvider: new GuidProviderMock(guid));
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.Join("member", false);
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.MemberJoined, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
            Assert.IsInstanceOfType(messages[0], typeof(ScrumTeamMemberMessage));
            var memberMessage = (ScrumTeamMemberMessage)messages[0];
            Assert.AreEqual<string>("member", memberMessage.MemberName);
            Assert.AreEqual<string>("Member", memberMessage.MemberType);
            Assert.AreEqual<Guid>(guid, memberMessage.SessionId);
        }

        [TestMethod]
        public void ObservableMessages_MemberDisconnected_ScrumTeamMemberMessage()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.Disconnect("master");
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.MemberDisconnected, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
            Assert.IsInstanceOfType(messages[0], typeof(ScrumTeamMemberMessage));
            var memberMessage = (ScrumTeamMemberMessage)messages[0];
            Assert.AreEqual<string>("master", memberMessage.MemberName);
            Assert.AreEqual<string>("ScrumMaster", memberMessage.MemberType);
        }

        [TestMethod]
        public void ObservableMessages_MemberUpdateActivity_ScrumTeamMemberMessage()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var target = CreateAzurePlanningPokerController(guidProvider: new GuidProviderMock(guid));
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.ScrumMaster!.AcknowledgeMessages(guid, 2);
            teamLock.Team.ScrumMaster.UpdateActivity();
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.MemberActivity, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
            Assert.IsInstanceOfType(messages[0], typeof(ScrumTeamMemberMessage));
            var memberMessage = (ScrumTeamMemberMessage)messages[0];
            Assert.AreEqual<string>("master", memberMessage.MemberName);
            Assert.AreEqual<string>("ScrumMaster", memberMessage.MemberType);
            Assert.AreEqual<Guid>(guid, memberMessage.SessionId);
            Assert.AreEqual<long>(2, memberMessage.AcknowledgedMessageId);
        }

        [TestMethod]
        public void ObservableMessages_EstimationStarted_ScrumTeamMessage()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.ScrumMaster!.StartEstimation();
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.EstimationStarted, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
        }

        [TestMethod]
        public void ObservableMessages_EstimationCanceled_ScrumTeamMessage()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);
            teamLock.Team.ScrumMaster!.StartEstimation();

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.ScrumMaster.CancelEstimation();
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.EstimationCanceled, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
        }

        [TestMethod]
        public void ObservableMessages_MemberEstimated_ScrumTeamMemberMessage()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);
            teamLock.Team.ScrumMaster!.StartEstimation();

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.ScrumMaster.Estimation = new Estimation(3.0);
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(2, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.MemberEstimated, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
            Assert.IsInstanceOfType(messages[0], typeof(ScrumTeamMemberEstimationMessage));
            var memberMessage = (ScrumTeamMemberEstimationMessage)messages[0];
            Assert.AreEqual<string>("master", memberMessage.MemberName);
            Assert.AreEqual<double?>(3.0, memberMessage.Estimation);
        }

        [TestMethod]
        public void ObservableMessages_TimerStarted_ScrumTeamTimerMessage()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            var target = CreateAzurePlanningPokerController(dateTimeProvider: dateTimeProvider);
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);
            var member = (Member)teamLock.Team.Join("testMember", false);
            dateTimeProvider.SetUtcNow(new DateTime(2021, 11, 16, 23, 49, 31, DateTimeKind.Utc));

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            member.StartTimer(TimeSpan.FromSeconds(112));
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.TimerStarted, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
            Assert.IsInstanceOfType(messages[0], typeof(ScrumTeamTimerMessage));
            var timerMessage = (ScrumTeamTimerMessage)messages[0];
            Assert.AreEqual<DateTime>(new DateTime(2021, 11, 16, 23, 51, 23, DateTimeKind.Utc), timerMessage.EndTime);
        }

        [TestMethod]
        public void ObservableMessages_TimerCanceled_ScrumTeamMessage()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            dateTimeProvider.SetUtcNow(new DateTime(2021, 11, 16, 23, 49, 31, DateTimeKind.Utc));
            var target = CreateAzurePlanningPokerController(dateTimeProvider: dateTimeProvider);
            target.EndInitialization();
            var messages = new List<ScrumTeamMessage>();
            var teamLock = target.CreateScrumTeam("test", "master", Deck.Standard);
            teamLock.Team.ScrumMaster!.StartTimer(TimeSpan.FromSeconds(112));
            dateTimeProvider.SetUtcNow(new DateTime(2021, 11, 16, 23, 50, 0, DateTimeKind.Utc));

            // Act
            target.ObservableMessages.Subscribe(m => messages.Add(m));
            teamLock.Team.ScrumMaster.CancelTimer();
            target.Dispose();

            // Verify
            Assert.AreEqual<int>(1, messages.Count);
            Assert.AreEqual<MessageType>(MessageType.TimerCanceled, messages[0].MessageType);
            Assert.AreEqual<string>("test", messages[0].TeamName);
        }

        [TestMethod]
        public void CreateScrumTeam_AfterInitialization_CreatesNewTeam()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();

            // Act
            var result = target.CreateScrumTeam("test", "master", Deck.Standard);

            // Verify
            Assert.IsTrue(target.IsInitialized);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Team);
            Assert.AreEqual<string>("test", result.Team.Name);
            Assert.IsNotNull(result.Team.ScrumMaster);
            Assert.AreEqual<string>("master", result.Team.ScrumMaster.Name);
        }

        [TestMethod]
        public void CreateScrumTeam_InitializationTeamListIsNotSet_WaitForInitializationTeamList()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();

            // Act
            var task = Task.Factory.StartNew<IScrumTeamLock>(
                () => target.CreateScrumTeam("test", "master", Deck.Standard),
                default(CancellationToken),
                TaskCreationOptions.None,
                TaskScheduler.Default);
            Assert.IsFalse(task.IsCompleted);
            Thread.Sleep(50);
            Assert.IsFalse(task.IsCompleted);
            target.SetTeamsInitializingList(Enumerable.Empty<string>());
            Assert.IsTrue(task.Wait(1000));

            // Verify
            Assert.IsNotNull(task.Result);
            Assert.IsFalse(target.IsInitialized);
        }

        [TestMethod]
        public void CreateScrumTeam_TeamNameIsInInitializationTeamList_ArgumentException()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.SetTeamsInitializingList(new string[] { "test" });

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.CreateScrumTeam("test", "master", Deck.Standard));
        }

        [TestMethod]
        public void CreateScrumTeam_InitializationTimeout_Exception()
        {
            // Arrange
            var configuration = new AzurePlanningPokerConfiguration() { InitializationTimeout = 1 };
            var target = CreateAzurePlanningPokerController(configuration: configuration);

            // Act
            Assert.ThrowsException<TimeoutException>(() => target.CreateScrumTeam("test", "master", Deck.Standard));
        }

        [TestMethod]
        public void GetScrumTeam_AfterInitialization_GetsExistingTeam()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.EndInitialization();
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("test team", "master", Deck.Standard))
            {
                team = teamLock.Team;
            }

            // Act
            var result = target.GetScrumTeam("test team");

            // Verify
            Assert.IsTrue(target.IsInitialized);
            Assert.AreEqual<ScrumTeam>(team, result.Team);
        }

        [TestMethod]
        public void GetScrumTeam_TeamIsNotInitialized_WaitForTeamInitialization()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.SetTeamsInitializingList(new string[] { "test team", "team2" });

            // Act
            var task = Task.Factory.StartNew<IScrumTeamLock>(() => target.GetScrumTeam("test team"), default(CancellationToken), TaskCreationOptions.None, TaskScheduler.Default);
            Assert.IsFalse(task.IsCompleted);
            Thread.Sleep(50);
            Assert.IsFalse(task.IsCompleted);
            target.InitializeScrumTeam(new ScrumTeam("test team"));
            Assert.IsTrue(task.Wait(1000));

            // Verify
            Assert.IsNotNull(task.Result);
            Assert.IsFalse(target.IsInitialized);
        }

        [TestMethod]
        public void GetScrumTeam_TeamIsNotWaitingForInitialization_ReturnsTeam()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.SetTeamsInitializingList(new string[] { "test team", "team2" });
            target.InitializeScrumTeam(new ScrumTeam("test team"));

            // Act
            var result = target.GetScrumTeam("test team");

            // Verify
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetScrumTeam_InitializationTimeout_ArgumentException()
        {
            // Arrange
            var configuration = new AzurePlanningPokerConfiguration() { InitializationTimeout = 1 };
            var target = CreateAzurePlanningPokerController(configuration: configuration);

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.GetScrumTeam("test team"));
        }

        [TestMethod]
        public void SetTeamsInitializingList_TeamSpeacified_DeleteAllFromRepository()
        {
            // Arrange
            var repository = new Mock<IScrumTeamRepository>(MockBehavior.Strict);
            repository.Setup(r => r.DeleteAll());
            var target = CreateAzurePlanningPokerController(repository: repository.Object);

            // Act
            target.SetTeamsInitializingList(new string[] { "team" });

            // Verify
            repository.Verify(r => r.DeleteAll());
            Assert.IsFalse(target.IsInitialized);
        }

        [TestMethod]
        public void SetTeamsInitializingList_AfterEndInitialization_NotDeleteAnythingFromRepository()
        {
            // Arrange
            var repository = new Mock<IScrumTeamRepository>(MockBehavior.Strict);
            var target = CreateAzurePlanningPokerController(repository: repository.Object);
            target.EndInitialization();

            // Act
            target.SetTeamsInitializingList(new string[] { "team" });

            // Verify
            repository.Verify(r => r.DeleteAll(), Times.Never());
            Assert.IsTrue(target.IsInitialized);
        }

        [TestMethod]
        public void InitializeScrumTeam_TeamSpeacified_TeamAddedToController()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            var team = new ScrumTeam("team");
            target.SetTeamsInitializingList(new string[] { "team" });
            Assert.IsFalse(target.IsInitialized);

            // Act
            target.InitializeScrumTeam(team);

            // Verify
            var result = target.GetScrumTeam("team");
            Assert.AreEqual<ScrumTeam>(team, result.Team);
            Assert.IsTrue(target.IsInitialized);
        }

        [TestMethod]
        public void InitializeScrumTeam_TeamSpecified_TeamCreatedMessageIsNotSent()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            var team = new ScrumTeam("team");
            target.SetTeamsInitializingList(new string[] { "team" });
            ScrumTeamMessage? message = null;
            target.ObservableMessages.Subscribe(m => message = m);

            // Act
            target.InitializeScrumTeam(team);

            // Verify
            Assert.IsNull(message);
        }

        [TestMethod]
        public void EndInitialization_TeamToInitialize_IsInitializedIsTrue()
        {
            // Arrange
            var target = CreateAzurePlanningPokerController();
            target.SetTeamsInitializingList(new string[] { "team" });
            Assert.IsFalse(target.IsInitialized);

            // Act
            target.EndInitialization();

            // Verify
            Assert.IsTrue(target.IsInitialized);
        }

        private static AzurePlanningPokerController CreateAzurePlanningPokerController(
            DateTimeProvider? dateTimeProvider = null,
            GuidProvider? guidProvider = null,
            DeckProvider? deckProvider = null,
            IAzurePlanningPokerConfiguration? configuration = null,
            IScrumTeamRepository? repository = null,
            TaskProvider? taskProvider = null,
            ILogger<Controllers.PlanningPokerController>? logger = null)
        {
            if (logger == null)
            {
                logger = Mock.Of<ILogger<Controllers.PlanningPokerController>>();
            }

            return new AzurePlanningPokerController(dateTimeProvider, guidProvider, deckProvider, configuration, repository, taskProvider, logger);
        }
    }
}
