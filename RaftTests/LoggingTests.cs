using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.TestExecutor;
using NSubstitute;
using NSubstitute.Routing.AutoValues;
using RaftLib;

namespace RaftTests;

public class LoggingTests
{
    public class TestClient : IClient
    {
        public async Task ResponseClientRequestRPC(bool isSuccess, string message)
        {
            await Task.CompletedTask;
        }

        public static TestClient Default {get => new TestClient();}
    }
    // Testing #1
    [Fact]
    public async Task GivenALeaderWhenWorkingWithAClientRequestItSendsAnAppendEntriesRPCToFollowers()
    {
        var moqNode = Substitute.For<INode>();
        moqNode.Id = 0;
        var leaderNode = new Node(1, [moqNode]);
        leaderNode.InitiateLeadership();

        leaderNode.ReceiveClientRequest(TestClient.Default, "test1", "test2");
        leaderNode.OtherNextIndexes[0] = 0;
        await Task.Delay(100);

        await moqNode.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(log => 
    log.entries != null && log.entries.Count() == 1
        ));
    }


    // Testing #2
    [Fact]
    public void GivenLeaderNodeWhenServicingClientRequestItPutsTheLogAtTheEnd()
    {
        var leaderNode = new Node();
        leaderNode.InitiateLeadership();

        leaderNode.ReceiveClientRequest(TestClient.Default, "test1", "value1");

        leaderNode.LogList[0].Term.Should().Be(0);
        leaderNode.LogList[0].Key.Should().Be("test1");
        leaderNode.LogList[0].Value.Should().Be("value1");

        leaderNode.ReceiveClientRequest(TestClient.Default, "test2", "value2");

        leaderNode.LogList[1].Term.Should().Be(0);
        leaderNode.LogList[1].Key.Should().Be("test2");
        leaderNode.LogList[1].Value.Should().Be("value2");
    }

    // Testing #3
    [Fact]
    public void GivenANewlyInitializedNodeItShouldBeEmpty()
    {
        var node = new Node();

        node.LogList.Count().Should().Be(0);
    }

    // Testing #4
    [Fact]
    public void GivenANewlyElectedLeaderTheyInitializeTheDictionaryOfNextIndexesToBeOneMoreThanItsLatest()
    {
        var node = new Node(1, TestNode.LargeCluster);

        node.InitiateLeadership();

        node.OtherNextIndexes[2].Should().Be(1);
        node.OtherNextIndexes.Values.All(x => x == 1).Should().BeTrue();

        node.ReceiveClientRequest(TestClient.Default, "key", "log");
        node.InitiateLeadership();

        node.OtherNextIndexes[2].Should().Be(2);
        node.OtherNextIndexes.Values.All(x => x == 2).Should().BeTrue();
    }

    // Testing #5
    [Fact]
    public void GivenALeaderNodeWhenSendingOutHeartbeatsEachNodeGivesLogsBasedOffOfTheirNextIndex()
    {
        var moqNode1 = Substitute.For<INode>();
        moqNode1.Id = 1;
        var moqNode2 = Substitute.For<INode>();
        moqNode2.Id = 2;
        var node = new Node(0, [moqNode1, moqNode2]);
        node.InitiateLeadership();

        var log1 = new Log(0, "Ballin", "Test");
        var log2 = new Log(0, "Ballin2", "Test2");
        node.LogList = [log1, log2];

        node.OtherNextIndexes[1] = 1;
        node.OtherNextIndexes[2] = 2;

        Thread.Sleep(75);

        moqNode1.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => 
            logs.entries.Count() == 1 &&
            logs.prevIndex == 1
        ));

        moqNode2.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => 
            logs.entries.Count() == 0 &&
            logs.prevIndex == 2
        ));


    }

    // Testing #6 
    [Fact]
    public async Task GivenALeaderWhenSendingOutHeartbeatsThenTheLeaderIncludesTheHighestCommittedEntry()
    {
        var moqNode1 = Substitute.For<INode>();
        moqNode1.Id = 2;
        var node = new Node(1, [moqNode1]);

        //Arrange
        node.InitiateLeadership();
        await moqNode1.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(log => 
            log.leaderId == 1 &&
            log.term == 0 &&
            log.commitIndex == 0 &&
            log.prevIndex == 1 &&
            log.prevTerm == 0
        ));
        node.ReceiveClientRequest(TestClient.Default, "test", "log");
        await node.ResponseAppendLogRPC(new (true, 2, 0, 1));
        await Task.Delay(75);

        //Assert
        await moqNode1.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(log => 
            log.leaderId == 1 &&
            log.term == 0 &&
            log.commitIndex == 1 &&
            log.prevIndex == 1 &&
            log.prevTerm == 0
        ));
    }

    // Testing #7
    [Fact]
    public async Task GivenAFollowerNodeWhenTheyGetAHeartbeatWithACommitIndexGreaterThanTheirCommittedIndexThenTheyCommitThoseLogsIntoTheirLocalState()
    {
        // Given
        var node = new Node(1);
        node.StopTimer();
    
        // When
        await node.RequestAppendLogRPC(new (1, 1, [new Log(1, "there", "their")], 1, 0, 0));
    
        // Then
        node.InternalStateMachine["there"].Should().Be("their");
    }

    // Testinng #8.a
    [Fact]
    public void GivenALeaderNodeWhenAClientRequestGetsCommitedItGetsAddedToTheStateMachine()
    {
        // Given
        var node = new Node(1);

        // When
        node.InitiateLeadership();
        node.ReceiveClientRequest(TestClient.Default, "hi", "there");

        // Then
        node.InternalStateMachine["hi"].Should().Be("there");
        node.InternalCommitIndex.Should().Be(1);
    }

    // Testing #8.b
    [Fact]
    public async Task GivenALeaderNodeWhenAClientRequestGetsCommitedItGetsAddedToTheStateMachineEvenInALargeCluster()
    {
        // Given
        var node = new Node(1, TestNode.LargeCluster);

        // When
        node.InitiateLeadership();
        node.ReceiveClientRequest(TestClient.Default, "hi", "there");
        node.InternalStateMachine.ContainsKey("hi").Should().BeFalse();

        await node.ResponseAppendLogRPC(new (true, 2, 0, 1));
        node.InternalStateMachine.ContainsKey("hi").Should().BeFalse();

        await node.ResponseAppendLogRPC(new (true, 2, 0, 1));

        // Then
        node.InternalStateMachine.ContainsKey("hi").Should().BeTrue();
        node.InternalStateMachine["hi"].Should().Be("there");
    }

    // Testing #9.a
    [Fact]
    public void GivenALeaderNodeWhenTheyRecieveAClientRequestAndTheyAreTheOnlyNodeInTheClusterItIsCommitedAndTheIndexGoesUp()
    {
        var node = new Node(1, []);
        node.InitiateLeadership();
        node.ReceiveClientRequest(TestClient.Default, "hi", "there");

        node.InternalCommitIndex.Should().Be(1);
    }

    // Testing #9.b
    [Fact]
    public async Task GivenALeaderNodeWhenTheyRecieveAClientRequestAndTheClusterSizeIs5InThen2NodesRespondWithSuccessThenTheIndexGoesUp()
    {
        var node = new Node(1, TestNode.LargeCluster);

        node.InitiateLeadership();
        node.ReceiveClientRequest(TestClient.Default, "hi", "there");
        node.InternalCommitIndex.Should().Be(0);

        await node.ResponseAppendLogRPC(new (true, 2, 0, 1));
        node.InternalCommitIndex.Should().Be(0);

        await node.ResponseAppendLogRPC(new (true, 3, 0, 1));
        node.InternalCommitIndex.Should().Be(1);
    }

    // Testing #10
    [Fact]
    public async Task GivenAFollowerNodeRecievesLogsToAppendTheyGetAddedToPersonalLogList()
    {
        // Given
        var node = new Node();
        node.StopTimer();
        var LogToAdd = new Log(1, "Key", "Value");

        // When
        await node.RequestAppendLogRPC(new (2, 1, [LogToAdd], 0, 0, 0));

        // Then
        node.LogList.Should().Contain(LogToAdd);
    }

    // Testing #11
    [Fact]
    public async Task GivenAFollowerNodeWhenRespondingToAnAppendEntriesThenItIncludesTheTermAndLogEntryIndex()
    {
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;
        var node = new Node(1, [moqLeader]);

        await node.RequestAppendLogRPC(new (2, 1, [], 0, 0, 0));
        await moqLeader.Received().ResponseAppendLogRPC(new (true, 1, 1, 0));

        await node.RequestAppendLogRPC(new (2, 1, [new Log(0, "blah", "blah")], 0, 0, 0));
        await moqLeader.Received().ResponseAppendLogRPC(new (true, 1, 1, 0));
    }

    // Testing #12.a
    [Fact]
    public void GivenALeaderNodeWhenAClientsRequestgetsFinishedTheygetNotified()
    {
        // Given
        var moqClient = Substitute.For<IClient>();
        var leaderNode = new Node(1);
        leaderNode.InitiateLeadership();

        // When
        moqClient.DidNotReceive().ResponseClientRequestRPC(true, Arg.Any<string>());
        leaderNode.ReceiveClientRequest(moqClient, "hi", "there");
    
        // Then
        moqClient.Received().ResponseClientRequestRPC(true, Arg.Any<string>());
    }

    // Testing #12.b
    [Fact]
    public async Task GivenALeaderNodeWhenALeaderFinallyReceivesAMajorityEnoughThenTheClientGetsNotifiedRightWhenTheLogGetsCommitted()
    {
        // Given
        var moqClient = Substitute.For<IClient>();
        var leaderNode = new Node(1, TestNode.LargeCluster);
        leaderNode.InitiateLeadership();
        leaderNode.ReceiveClientRequest(moqClient, "blah", "blah");
        await moqClient.DidNotReceive().ResponseClientRequestRPC(Arg.Any<bool>(), Arg.Any<string>());
        leaderNode.Majority.Should().Be(3);

        // When
        await leaderNode.ResponseAppendLogRPC(new (true, 2, 0, 1));
        await moqClient.DidNotReceive().ResponseClientRequestRPC(Arg.Any<bool>(), Arg.Any<string>());
    
        await leaderNode.ResponseAppendLogRPC(new (true, 3, 0, 1));

        // Then
        leaderNode.LogReplicated[0].Should().Be(3);
        leaderNode.InternalStateMachine["blah"].Should().Be("blah");
        await moqClient.Received().ResponseClientRequestRPC(true, Arg.Any<string>());
    }

    // Testing #13
    [Fact]
    public void GivenALeaderNodeWhenALogIsCommittedItGetsAddedToTheStateMachine()
    {
        // Given
        var node = new Node(1);

        // When
        node.InitiateLeadership();
        node.ReceiveClientRequest(TestClient.Default, "test", "machine");

        // Then
        node.InternalStateMachine["test"].Should().Be("machine");
    }

    // Testing #14
    [Fact]
    public async Task GivenAFollowerNodeWhenTheyReceiveWordThatTheNextCommittIndexIsHigherThenTheirCommittIndexAlsoGoesUp()
    {
        // Given
        var node = new Node(1);
        node.StopTimer();

        // When
        await node.RequestAppendLogRPC(new (1, 1, [new Log(1, "hi", "there")], 1, 0, 0));

        // Then
        node.InternalCommitIndex.Should().Be(1);
        node.InternalStateMachine["hi"].Should().Be("there");
    }

    // Testing #15.a
    [Fact]
    public void GivenALeaderNodeWhenSendingHeartbeatsThenIncludeTheTermAndIndexOfTheLastLog()
    {
        // Given
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 2;
        var leader = new Node(1, [moqFollower]);
        leader.InitiateLeadership();
        moqFollower.Received().RequestAppendLogRPC(new (1, 0, [], 0, 1, 0));

        // When
        leader.ReceiveClientRequest(TestClient.Default, "test", "test");
        Thread.Sleep(75);

        // Then
        moqFollower.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => logs.prevIndex == 1));
    }

    // Testing 15.b
    [Fact]
    public async Task GivenAFollowerNodeWhenThePrevIndexOfAnAppendEntriesRPCDoesNotMatchTheInternalLogThenItRespondsWithFalse()
    {
        // Given
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;
        var node = new Node(1, [moqLeader]);
        node.StopTimer();

        // When
        await node.RequestAppendLogRPC(new (2, 0, [], 0, 1, 0));

        // Then
        await moqLeader.Received().ResponseAppendLogRPC(Arg.Is<ResponseAppendLogDto>(vote => vote.ableToSync == false));
    }

    // Testing 15.c
    [Fact]
    public async Task GivenAFollowerNodeWhenTheyHaveOutOfDateLogsAndALeaderTellsThemToAppendALogAtALowerIndexTheyDeleteEverythingAboveWhereTheyNeedToAndRespondFalse()
    {
        // Given
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;
        var node = new Node(1, [moqLeader]);
        node.StopTimer();
        node.LogList = [new Log(0, "Im", "Outdated")];

        // When
        await node.RequestAppendLogRPC(new (2, 1, [], 0, 0, 0));
    
        // Then
        node.LogList.Should().BeEmpty();
        await moqLeader.Received().ResponseAppendLogRPC(new (true, 1, 1, 0));
    }

    // Testing 15.d
    [Fact]
    public async Task GivenALeaderWhenItRecievesAFailingResponseItDecrementsThenNextIndexIsChangedInTheDict()
    {
        // Given
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 1;
        var leaderNode = new Node(2, [moqFollower]);
        leaderNode.InitiateLeadership();
        leaderNode.ReceiveClientRequest(TestClient.Default, "Hi", "There");
        leaderNode.ReceiveClientRequest(TestClient.Default, "Hi2", "There2");
        leaderNode.InitiateLeadership();


        // When
        leaderNode.OtherNextIndexes[1].Should().Be(3);
        await leaderNode.ResponseAppendLogRPC(new (false, 1, 2, 0));

        // Then
        leaderNode.OtherNextIndexes[1].Should().Be(2);
    }

    // Testing #15.e
    [Fact]
    public async Task GivenALeaderNodeWhenSendingOutHeartbeatsThePrevIndexFromCorrectLog()
    {
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 1;
        var leaderNode = new Node(2,[moqFollower]);
        leaderNode.InitiateLeadership();
        leaderNode.ReceiveClientRequest(TestClient.Default, "test", "test");
        leaderNode.ReceiveClientRequest(TestClient.Default, "test", "test");
        leaderNode.OtherNextIndexes[1] = 2;

        Thread.Sleep(75);

        await moqFollower.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => logs.prevIndex == 1));
    }

    // Testing #15.f
    [Fact]
    public async Task GivenALeaderNodeWhenSendingOutHeartbeatsPrevTermFromCorrectLog()
    {
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 1;
        var leaderNode = new Node(2,[moqFollower]);
        leaderNode.InitiateLeadership();
        leaderNode.CurrentTerm = 2;
        leaderNode.ReceiveClientRequest(TestClient.Default, "test", "test");
        leaderNode.ReceiveClientRequest(TestClient.Default, "test", "test");
        leaderNode.OtherNextIndexes[1] = 2;

        Thread.Sleep(75);

        await moqFollower.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => logs.prevTerm == 2 && logs.prevIndex == 2));
    }

    // Testing #16
    [Fact]
    public void GivenALeaderNodeWithAnUncommittedLogTheLogStaysUncommittedUntilTheyRecieveAMajority()
    {
        // Given
        var leaderNode = new Node(2, TestNode.LargeCluster);
        leaderNode.InitiateLeadership();
        leaderNode.ReceiveClientRequest(new TestClient(), "test", "test");
    
        // When
        Thread.Sleep(75);
        //No Responses
    
        // Then
        leaderNode.InternalCommitIndex.Should().Be(0);
        leaderNode.InternalStateMachine.ContainsKey("test").Should().BeFalse();
    }

    // Testing #17
    [Fact]
    public void GivenALeaderThenTheLeaderSendsTheRequestToAppendLogsConstantly()
    {
        // Given
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 2;
        var leaderNode = new Node(1, [moqFollower]);
        leaderNode.InitiateLeadership();
        leaderNode.ReceiveClientRequest(new TestClient(), "hi", "there");
        leaderNode.ReceiveClientRequest(new TestClient(), "hi2", "there2");

        // When
        Thread.Sleep(60);
        moqFollower.Received().RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => 
            logs.entries.Count() == 1 &&
            logs.prevIndex == 1
        ));    

        Thread.Sleep(60);

        // Then
        moqFollower.Received(2).RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(logs => 
            logs.entries.Count() == 1 &&
            logs.prevIndex == 1
        ));    
 

    }

    // Testing #18
    [Fact]
    public void GivenALeaderNodeWithAnUncommittedLogTheClientDoesntGetAResponseUntilTheyRecieveAMajority()
    {
        // Given
        var leaderNode = new Node(2, TestNode.LargeCluster);
        leaderNode.InitiateLeadership();
        var moqClient = Substitute.For<IClient>();
        leaderNode.ReceiveClientRequest(moqClient, "test", "test");
    
        // When
        //No Responses
    
        // Then
        leaderNode.InternalCommitIndex.Should().Be(0);
        moqClient.DidNotReceive().ResponseClientRequestRPC(Arg.Any<bool>(), Arg.Any<string>());
    }

    // Testing #19
    [Fact]
    public async Task GivenAFollowerNodeRecievesAnAppendEntriesRPCTooFarIntoTheFutureTheyRejectTheTest()
    {
        // Given
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;
        var node = new Node(1, [moqLeader]);
        node.StopTimer();
    
        // When
        await node.RequestAppendLogRPC(new (2, 1, [new Log(1, "grab", "bage")], 0, 3, 3));
    
        // Then
        await moqLeader.Received().ResponseAppendLogRPC(new (false, 1, 1, 0));
    }

    // Testing #20
    [Fact]
    public async Task GivenAFollowerNodeWhenReceivingAppendLogRPCTheyWillOnlyRespondTrueIfTheyFindAValidPrevTermAndPrevIndex()
    {
        // Given
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;
        var node = new Node(1, [moqLeader]);
        node.StopTimer();
    
        // When
        await node.RequestAppendLogRPC(new (2, 1, [new Log(1, "grab", "bage")], 0, 3, 1));
        await moqLeader.Received().ResponseAppendLogRPC(new (false, 1, 1, 0));

        await node.RequestAppendLogRPC(new (2, 1, [new Log(1, "grab", "bage"), new Log(1, "grab2", "bage2")], 0, 2, 1));
        await moqLeader.Received(2).ResponseAppendLogRPC(new (false, 1, 1, 0));

        await node.RequestAppendLogRPC(new (2, 1, [new Log(1, "grab", "bage"), new Log(1, "grab2", "bage2"), new Log(1, "grab3", "bage3")], 0, 1, 1));
        await moqLeader.Received(3).ResponseAppendLogRPC(new (false, 1, 1, 0));

        await node.RequestAppendLogRPC(new (2, 1, [new Log(1, "grab", "bage"), new Log(1, "grab2", "bage2"), new Log(1, "grab3", "bage3")], 0, 0, 0));
        await moqLeader.Received().ResponseAppendLogRPC(new (true, 1, 1, 3));
    }

    // Testing NaN
    [Fact]
    public void GivenAFollowerNodeWhenServicingAClientRequestTheySendRespondWithAFalse()
    {
        // Given
        var moqClient = Substitute.For<IClient>();
        var node = new Node(1);
        node.StopTimer();
    
        // When
        node.ReceiveClientRequest(moqClient, "Give", "me");
    
        // Then
        moqClient.Received().ResponseClientRequestRPC(false, Arg.Any<string>());
    }

    // Testing Leader Sends Correct Logs to followers
    [Fact]
    public void GivenALeaderNodeWhenRecievingAClientRequestThenTheySendTheCorrectRequest()
    {
        var moqNode1 = Substitute.For<INode>();
        moqNode1.Id = 1;
        var node = new Node(2, [moqNode1]);
        node.InitiateLeadership();

        // When
        node.ReceiveClientRequest(new TestClient(), "hi", "there");
        node.OtherNextIndexes[1] = 0;
        Thread.Sleep(60);

        // Then
        moqNode1.Received(1).RequestAppendLogRPC(Arg.Is<RequestAppendLogDto>(log => 
        log.term == 0 &&
        log.leaderId == 2 &&
        log.entries.Length == 1 &&
        log.prevIndex == 0 &&
        log.prevTerm == 0 &&
        log.commitIndex == 0));
    }
}