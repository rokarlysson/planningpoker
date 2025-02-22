﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Duracellko.PlanningPoker.Controllers;
using Duracellko.PlanningPoker.Domain;
using Duracellko.PlanningPoker.Domain.Test;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Duracellko.PlanningPoker.Test.Controllers
{
    [TestClass]
    public class PlanningPokerControllerTest
    {
        [TestMethod]
        public void PlanningPokerController_Create_DefaultDateTimeProvider()
        {
            // Act
            var result = CreatePlanningPokerController();

            // Verify
            Assert.AreEqual<DateTimeProvider>(DateTimeProvider.Default, result.DateTimeProvider);
        }

        [TestMethod]
        public void PlanningPokerController_SpecificDateTimeProvider_DateTimeProviderIsSet()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();

            // Act
            var result = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);

            // Verify
            Assert.AreEqual<DateTimeProvider>(dateTimeProvider, result.DateTimeProvider);
        }

        [TestMethod]
        public void PlanningPokerController_Create_DefaultGuidProvider()
        {
            // Act
            var result = CreatePlanningPokerController();

            // Verify
            Assert.AreEqual<GuidProvider>(GuidProvider.Default, result.GuidProvider);
        }

        [TestMethod]
        public void PlanningPokerController_SpecificGuidProvider_GuidProviderIsSet()
        {
            // Arrange
            var guidProvider = new GuidProviderMock();

            // Act
            var result = CreatePlanningPokerController(guidProvider: guidProvider);

            // Verify
            Assert.AreEqual<GuidProvider>(guidProvider, result.GuidProvider);
        }

        [TestMethod]
        public void PlanningPokerController_Configuration_ConfigurationIsSet()
        {
            // Arrange
            var configuration = new Duracellko.PlanningPoker.Configuration.PlanningPokerConfiguration();

            // Act
            var result = CreatePlanningPokerController(configuration: configuration);

            // Verify
            Assert.AreEqual(configuration, result.Configuration);
        }

        [TestMethod]
        public void ScrumTeamNames_Get_ReturnsListOfTeamNames()
        {
            // Arrange
            var target = CreatePlanningPokerController();
            using (var teamLock1 = target.CreateScrumTeam("team1", "master1", Deck.Standard))
            {
            }

            using (var teamLock2 = target.CreateScrumTeam("team2", "master1", Deck.Standard))
            {
            }

            // Act
            var result = target.ScrumTeamNames;

            // Verify
            var expectedCollection = new string[] { "team1", "team2" };
            CollectionAssert.AreEquivalent(expectedCollection, result.ToList());
        }

        [TestMethod]
        public void CreateScrumTeam_TeamName_CreatedTeamWithSpecifiedName()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                // Verify
                Assert.IsNotNull(teamLock);
                Assert.IsNotNull(teamLock.Team);
                Assert.AreEqual<string>("team", teamLock.Team.Name);
            }
        }

        [TestMethod]
        public void CreateScrumTeam_ScrumMasterName_CreatedTeamWithSpecifiedScrumMaster()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                // Verify
                Assert.IsNotNull(teamLock.Team.ScrumMaster);
                Assert.AreEqual<string>("master", teamLock.Team.ScrumMaster.Name);
            }
        }

        [TestMethod]
        public void CreateScrumTeam_StandardDeck_CreatedTeamWithStandardEstimations()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                // Verify
                var expectedCollection = new double?[]
                {
                    0.0, 0.5, 1.0, 2.0, 3.0, 5.0, 8.0, 13.0, 20.0, 40.0, 100.0, double.PositiveInfinity, null
                };
                var availableEstimations = teamLock.Team.AvailableEstimations.Select(e => e.Value).ToList();
                CollectionAssert.AreEquivalent(expectedCollection, availableEstimations);
            }
        }

        [TestMethod]
        public void CreateScrumTeam_FibonacciDeck_CreatedTeamWithFibonacciEstimations()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Fibonacci))
            {
                // Verify
                var expectedCollection = new double?[]
                {
                    0.0, 1.0, 2.0, 3.0, 5.0, 8.0, 13.0, 21.0, 34.0, 55.0, 89.0, double.PositiveInfinity, null
                };
                var availableEstimations = teamLock.Team.AvailableEstimations.Select(e => e.Value).ToList();
                CollectionAssert.AreEquivalent(expectedCollection, availableEstimations);
            }
        }

        [TestMethod]
        public void CreateScrumTeam_RockPaperScissorsLizardSpock_CreatedTeam()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.RockPaperScissorsLizardSpock))
            {
                // Verify
                var expectedCollection = new double?[]
                {
                    -999909.0, -999908.0, -999907.0, -999906.0, -999905.0
                };
                var availableEstimations = teamLock.Team.AvailableEstimations.Select(e => e.Value).ToList();
                CollectionAssert.AreEquivalent(expectedCollection, availableEstimations);
            }
        }

        [TestMethod]
        public void CreateScrumTeam_TeamNameAlreadyExists_ArgumentException()
        {
            // Arrange
            var target = CreatePlanningPokerController();
            var team = target.CreateScrumTeam("team", "master", Deck.Standard);
            team.Dispose();

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.CreateScrumTeam("team", "master2", Deck.Standard));
        }

        [TestMethod]
        public void CreateScrumTeam_TeamNameIsEmpty_ArgumentNullException()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => target.CreateScrumTeam(string.Empty, "master", Deck.Standard));
        }

        [TestMethod]
        public void CreateScrumTeam_ScrumMasterNameIsEmpty_ArgumentNullException()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => target.CreateScrumTeam("test team", string.Empty, Deck.Standard));
        }

        [TestMethod]
        public void CreateScrumTeam_SpecificDateTimeProvider_CreatedTeamWithDateTimeProvider()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            var target = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);

            // Act
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                // Verify
                Assert.AreEqual<DateTimeProvider>(dateTimeProvider, teamLock.Team.DateTimeProvider);
            }
        }

        [TestMethod]
        public void AttachScrumTeam_ScrumTeam_TeamIsInCollection()
        {
            // Arrange
            var team = new ScrumTeam("test team");
            var target = CreatePlanningPokerController();

            // Act
            target.AttachScrumTeam(team);

            // Verify
            Assert.IsNotNull(target.GetScrumTeam(team.Name));
        }

        [TestMethod]
        public void AttachScrumTeam_ScrumTeam_ReturnsSameTeam()
        {
            // Arrange
            var team = new ScrumTeam("test team");
            var target = CreatePlanningPokerController();

            // Act
            var result = target.AttachScrumTeam(team);

            // Verify
            Assert.AreEqual<ScrumTeam>(team, result.Team);
        }

        [TestMethod]
        public void AttachScrumTeam_TeamNameAlreadyExists_ArgumentException()
        {
            // Arrange
            var target = CreatePlanningPokerController();
            var existingTeam = target.CreateScrumTeam("team", "master", Deck.Standard);
            existingTeam.Dispose();
            var team = new ScrumTeam("team");

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.AttachScrumTeam(team));
        }

        [TestMethod]
        public void AttachScrumTeam_Null_ArgumentNullException()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => target.AttachScrumTeam(null!));
        }

        [TestMethod]
        public void GetScrumTeam_TeamNameExists_ReturnsExistingTeam()
        {
            // Arrange
            var target = CreatePlanningPokerController();
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                team = teamLock.Team;
            }

            // Act
            using (var teamLock = target.GetScrumTeam("team"))
            {
                // Verify
                Assert.AreEqual<ScrumTeam>(team, teamLock.Team);
            }
        }

        [TestMethod]
        public void GetScrumTeam_TeamNameNotExists_ArgumentException()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.GetScrumTeam("team"));
        }

        [TestMethod]
        public void GetScrumTeam_TeamNameIsEmpty_ArgumentNullException()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => target.GetScrumTeam(string.Empty));
        }

        [TestMethod]
        public void GetScrumTeam_AfterDisconnectingScrumMaster_ArgumentException()
        {
            // Arrange
            var target = CreatePlanningPokerController();
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                var team = teamLock.Team;
            }

            using (var teamLock = target.GetScrumTeam("team"))
            {
                var team = teamLock.Team;
                team.Disconnect("master");
            }

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.GetScrumTeam("team"));
        }

        [TestMethod]
        public void GetScrumTeam_AfterDisconnectingAllMembers_ArgumentException()
        {
            // Arrange
            var target = CreatePlanningPokerController();
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                var team = teamLock.Team;
                team.Join("member", false);
            }

            using (var teamLock = target.GetScrumTeam("team"))
            {
                var team = teamLock.Team;
                team.Disconnect("master");
            }

            using (var teamLock = target.GetScrumTeam("team"))
            {
                var team = teamLock.Team;
                team.Disconnect("member");
            }

            // Act
            Assert.ThrowsException<ArgumentException>(() => target.GetScrumTeam("team"));
        }

        [TestMethod]
        public void GetMessagesAsync_ObserverIsNull_ArgumentNullException()
        {
            // Arrange
            var target = CreatePlanningPokerController();

            // Act
            Assert.ThrowsException<ArgumentNullException>(() => target.GetMessagesAsync(null!, default(CancellationToken)));
        }

        [TestMethod]
        public void GetMessagesAsync_ScrumMasterHas2Messages_Returns2Messages()
        {
            IEnumerable<Message> result;

            // Arrange
            var target = CreatePlanningPokerController();
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Fibonacci))
            {
                teamLock.Lock();

                teamLock.Team.Join("member", false);
                teamLock.Team.ScrumMaster!.StartEstimation();

                // Act
                var messagesTask = target.GetMessagesAsync(teamLock.Team.ScrumMaster, default(CancellationToken));
                result = messagesTask.Result;
            }

            // Verify
            var messages = result.ToList();
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual(MessageType.MemberJoined, messages[0].MessageType);
            Assert.AreEqual(MessageType.EstimationStarted, messages[1].MessageType);
        }

        [TestMethod]
        public async Task GetMessagesAsync_MemberHasNoMessages_Returns1MessageAfterReceiving()
        {
            Task<IEnumerable<Message>> messagesTask;

            // Arrange
            var target = CreatePlanningPokerController();
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                teamLock.Lock();
                var member = teamLock.Team.Join("member", false);

                // Act
                messagesTask = target.GetMessagesAsync(member, default(CancellationToken));
            }

            Assert.IsFalse(messagesTask.IsCompleted);

            using (var teamLock = target.GetScrumTeam("team"))
            {
                teamLock.Lock();
                teamLock.Team.ScrumMaster!.StartEstimation();
            }

            Assert.IsTrue(messagesTask.IsCompleted);

            var result = await messagesTask;

            // Verify
            var messages = result.ToList();
            Assert.AreEqual(1, messages.Count);
            Assert.AreEqual(MessageType.EstimationStarted, messages[0].MessageType);
        }

        [TestMethod]
        public async Task GetMessagesAsync_TaskIsCancelled_ThrowsTaskCancelledException()
        {
            using (var cancellationToken = new CancellationTokenSource())
            {
                Task<IEnumerable<Message>> messagesTask;

                // Arrange
                var target = CreatePlanningPokerController();
                using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
                {
                    teamLock.Lock();

                    // Act
                    messagesTask = target.GetMessagesAsync(teamLock.Team.ScrumMaster!, cancellationToken.Token);
                }

                cancellationToken.Cancel();
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => messagesTask);
            }
        }

        [TestMethod]
        public async Task GetMessagesAsync_OperationTimesOut_ReturnsEmptyCollection()
        {
            Task<IEnumerable<Message>> messagesTask;

            // Arrange
            var waitForMessageTimeout = TimeSpan.FromSeconds(30);
            var delayTask = new TaskCompletionSource<object?>();
            var taskProvider = new Mock<TaskProvider>(MockBehavior.Strict);
            taskProvider.Setup(p => p.Delay(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .Returns(delayTask.Task);
            var configuration = new Mock<Configuration.IPlanningPokerConfiguration>(MockBehavior.Strict);
            configuration.SetupGet(c => c.WaitForMessageTimeout).Returns(waitForMessageTimeout);

            var target = CreatePlanningPokerController(configuration: configuration.Object, taskProvider: taskProvider.Object);

            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                teamLock.Lock();

                // Act
                messagesTask = target.GetMessagesAsync(teamLock.Team.ScrumMaster!, default(CancellationToken));
            }

            Assert.IsFalse(messagesTask.IsCompleted);

            delayTask.SetResult(null);

            Assert.IsTrue(messagesTask.IsCompleted);

            var result = await messagesTask;

            // Verify
            Assert.IsFalse(result.Any());
        }

        [TestMethod]
        public void DisconnectInactiveObservers_NoInactiveMembers_TeamIsUnchanged()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 20));

            var target = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                team = teamLock.Team;
                team.Join("member", false);
            }

            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 40));
            team.ScrumMaster!.UpdateActivity();

            // Act
            target.DisconnectInactiveObservers(TimeSpan.FromSeconds(30.0));

            // Verify
            using (var teamLock = target.GetScrumTeam("team"))
            {
                Assert.AreEqual<int>(2, teamLock.Team.Members.Count());
            }
        }

        [TestMethod]
        public void DisconnectInactiveObservers_InactiveMember_MemberIsDisconnected()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 20));

            var target = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Fibonacci))
            {
                team = teamLock.Team;
                team.Join("member", false);
            }

            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 55));
            team.ScrumMaster!.UpdateActivity();

            // Act
            target.DisconnectInactiveObservers(TimeSpan.FromSeconds(30.0));

            // Verify
            using (var teamLock = target.GetScrumTeam("team"))
            {
                Assert.AreEqual<int>(1, teamLock.Team.Members.Count());
            }
        }

        [TestMethod]
        public void DisconnectInactiveObservers_NoInactiveObservers_TeamIsUnchanged()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 20));

            var target = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                team = teamLock.Team;
                team.Join("observer", true);
            }

            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 40));
            team.ScrumMaster!.UpdateActivity();

            // Act
            target.DisconnectInactiveObservers(TimeSpan.FromSeconds(30.0));

            // Verify
            using (var teamLock = target.GetScrumTeam("team"))
            {
                Assert.AreEqual<int>(1, teamLock.Team.Observers.Count());
            }
        }

        [TestMethod]
        public void DisconnectInactiveObservers_InactiveObserver_ObserverIsDisconnected()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 20));

            var target = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                team = teamLock.Team;
                team.Join("observer", true);
            }

            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 55));
            team.ScrumMaster!.UpdateActivity();

            // Act
            target.DisconnectInactiveObservers(TimeSpan.FromSeconds(30.0));

            // Verify
            using (var teamLock = target.GetScrumTeam("team"))
            {
                Assert.AreEqual<int>(0, teamLock.Team.Observers.Count());
            }
        }

        [TestMethod]
        public void DisconnectInactiveObservers_InactiveScrumMaster_TeamIsClosed()
        {
            // Arrange
            var dateTimeProvider = new DateTimeProviderMock();
            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 20));

            var target = CreatePlanningPokerController(dateTimeProvider: dateTimeProvider);
            ScrumTeam team;
            using (var teamLock = target.CreateScrumTeam("team", "master", Deck.Standard))
            {
                team = teamLock.Team;
            }

            dateTimeProvider.SetUtcNow(new DateTime(2012, 1, 1, 3, 2, 55));

            // Act
            target.DisconnectInactiveObservers(TimeSpan.FromSeconds(30.0));

            // Verify
            Assert.ThrowsException<ArgumentException>(() => target.GetScrumTeam("team"));
        }

        private static PlanningPokerController CreatePlanningPokerController(
            DateTimeProvider? dateTimeProvider = null,
            GuidProvider? guidProvider = null,
            DeckProvider? deckProvider = null,
            Configuration.IPlanningPokerConfiguration? configuration = null,
            PlanningPoker.Data.IScrumTeamRepository? repository = null,
            TaskProvider? taskProvider = null,
            ILogger<PlanningPokerController>? logger = null)
        {
            if (logger == null)
            {
                var loggerMock = new Mock<ILogger<PlanningPokerController>>();
                logger = loggerMock.Object;
            }

            return new PlanningPokerController(dateTimeProvider, guidProvider, deckProvider, configuration, repository, taskProvider, logger);
        }
    }
}
