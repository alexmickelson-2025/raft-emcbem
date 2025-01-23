using FluentAssertions;
using NSubstitute;
using RaftLib;

namespace RaftTests;

public class TestNode : INode
{
    public int Id { get; set;}
    public TestNode(int id)
    {
        Id = id;
    }

    public async Task RequestVoteRPC(int candidateId, int termToVoteFor)
    {
        await Task.CompletedTask;
    }

    public async Task ResponseVoteRPC(bool result, int termToVoteFor)
    {
        await Task.CompletedTask;
    }

    public async Task ResponseAppendLogRPC(bool ableToSync)
    {
        await Task.CompletedTask;
    }


    public async Task RequestAppendLogRPC(int leaderId, int term, Log[] entries)
    {
         await Task.CompletedTask;
    }

    public static INode[] LargeCluster { get; set; } = [new TestNode(2), new TestNode(3), new TestNode(4), new TestNode(5)];

}

public class ElectionTests
{
    public INode[] LargeCluster { get; set; } = [new TestNode(2), new TestNode(3), new TestNode(4), new TestNode(5)];

    // Testing #3
    // Internal Count 1
    [Fact]
    public void GivenANodeWhenItHasJustBeenMadeThenItShouldBeAFollower()
    {
        Node node = new Node();

        node.CurrentState.Should().Be(NodeState.Follower);
    }

    // Testing #4
    // Internal Count 2
    [Fact]
    public void GivenAFollowerNodeWhenItDoesntRecieveAnyHeartbeatsIn150to300msItStartsAnElection()
    {
        Node node = new Node(1, LargeCluster);

        Thread.Sleep(320);

        node.CurrentState.Should().Be(NodeState.Candidate);
    }

    // Testing #6
    // Internal Count 3.a
    [Fact]
    public void GivenAFollowerBecomesANewCandidateThenTheInternalTermCounterGoesUpForEachCanidacyThatHappens()
    {
        Node node = new Node();

        Thread.Sleep(320);

        node.CurrentTerm.Should().BeGreaterThan(0);
    }

    // Internal Count 3.b
    [Fact]
    public void GivenAFollowerWhenANodeGetsForcedToIncreaseInTermItOnlyGoesUpOnce()
    {
        Node node = new Node(1, LargeCluster);

        node.InitiateCanidacy();

        node.CurrentTerm.Should().Be(1);
    }

    // Testing #11
    // Internal Count 4
    [Fact]
    public void GivenANodeJustTurnedToACandidateThenItShouldVoteForItselfForThatTerm()
    {
        Node node = new Node(1);

        Thread.Sleep(320);

        node.WhoDidIVoteFor[node.CurrentTerm].Should().Be(1);
    }

    // Testing #16
    // Internal Count 5
    [Fact]
    public async Task GivenACanidateNodeWhenTheElectionTimerEndsAnotherElectionBegins()
    {
        Node node = new Node(1, LargeCluster);

        Thread.Sleep(320);

        var firstWaitedTerm = node.CurrentTerm;
        
        await Task.Delay(620);

        node.CurrentState.Should().Be(NodeState.Candidate);
        firstWaitedTerm.Should().BeLessThan(node.CurrentTerm);

        node.WhoDidIVoteFor[node.CurrentTerm].Should().Be(1);
    }

    // Testing #8
    // Internal Count 6.a
    [Fact]
    public void GivenACandidateHasAMajorityOfVotesThenTheyBecomeALeader()
    {
        Node node = new Node(1);

        Thread.Sleep(320);

        node.CurrentState.Should().Be(NodeState.Leader);
    }

    // Internal Count 6.b
    [Fact]
    public async Task GivenACandidateHasToGet3VotesForMajorityAndTheyGet3VotesTheyBecomeLeader()
    {
        Node node = new Node(1, LargeCluster);

        node.InitiateCanidacy();

        node.MajorityVotesNeeded.Should().Be(3);
        node.CurrentState.Should().Be(NodeState.Candidate);
        node.CurrentVotesForTerm[node.CurrentTerm].Should().Be(1);

        await node.ResponseVoteRPC(true, node.CurrentTerm);
        node.CurrentVotesForTerm[node.CurrentTerm].Should().Be(2);

        await node.ResponseVoteRPC(true, node.CurrentTerm);
        node.CurrentVotesForTerm[node.CurrentTerm].Should().Be(3);

        node.CurrentState.Should().Be(NodeState.Leader);
    }

    // Testing #5
    // Internal Count 7
    [Fact]
    public void GivenACandidateWhoIsInLimboWheneverTheElectionTimeoutEndsANewTimerWIthATimeBetween150To300BeginsRANDOMLYMultipleTimes()
    {
        Node node = new Node(1, LargeCluster);

        var exactSameCount = 0;
        for (int i = 0; i < 10; i++)
        {
            var originalInterval = node.internalTimer?.Interval;

            Thread.Sleep(350);
            if(originalInterval == node.internalTimer?.Interval)
            {
                exactSameCount++;
            } 
        }

        exactSameCount.Should().BeLessThan(7);
    }

    // Testing #10
    // Internal Count 8.a
    [Fact]
    public async Task GivenAFollowerIsRequestedForAVoteThenTheyWillRespondWIthAYesSinceTheyHaventVotedForThisTermYet()
    {
        var leader = Substitute.For<INode>();
        leader.Id = 2;
        var mockedCluster = new INode[]{leader};

        var node = new Node(1, mockedCluster);

        await node.RequestVoteRPC(2, 1);

        await leader.Received().ResponseVoteRPC(true, 1);
    }

    // Internal Count 8.b
    [Fact]
    public async Task GivenAFollowerTheyDoNotVoteYesForATermBeforeOrDuringTheirCurrentTerm()
    {
        var node2 = Substitute.For<INode>();
        node2.Id = 2;
        var mockedCluster = new INode[]{node2};

        var node = new Node(1, mockedCluster);

        await node.RequestVoteRPC(2, 0);
        await node.RequestVoteRPC(2, -1);

        await node2.DidNotReceive().ResponseVoteRPC(true, 1);
        await node2.DidNotReceive().ResponseVoteRPC(true, 2);

    }

    // Testing #14
    // Internal Count 9
    [Fact]
    public async Task GivenAFollowerWhenItGetsRequestedToVoteTwiceForTheSameTermItWIllOnlySayYesToOneAndNotBoth()
    {
        var node2 = Substitute.For<INode>();
        node2.Id = 2;
        var node3 = Substitute.For<INode>();
        node3.Id = 3;
        var mockedCluster = new INode[]{node2, node3};
        var node = new Node(1, mockedCluster);

        await node.RequestVoteRPC(2, 1);
        await node.RequestVoteRPC(3, 1);


        await node2.Received().ResponseVoteRPC(true, 1);
        await node3.Received().ResponseVoteRPC(false, 1);
    }

    // Testing NOTHING APPARENTLY
    [Fact]
    public async Task GivenACandidateNodeTheyRequestVotesFromEveryone()
    {
        var node2 = Substitute.For<INode>();
        node2.Id = 2;
        var node3 = Substitute.For<INode>();
        node3.Id = 3;
        var mockedCluster = new INode[]{node2, node3};
        var node = new Node(1, mockedCluster);

        Thread.Sleep(320);

        await node2.Received().RequestVoteRPC(1, node.CurrentTerm);
        await node3.Received().RequestVoteRPC(1, node.CurrentTerm);
    }

    // Testing 9
    // Internal Count 10
    [Fact]
    public async Task GivenACandidateWhenTheyRecieveOnlyAFewVotesAndSomeAreUnresponsiveItWillStillTurnToBeingALeaderIfMajority()
    {
        Node node = new Node(1, LargeCluster);

        node.InitiateCanidacy();

        node.MajorityVotesNeeded.Should().Be(3);
        node.CurrentState.Should().Be(NodeState.Candidate);
        node.CurrentVotesForTerm[node.CurrentTerm].Should().Be(1);

        await node.ResponseVoteRPC(true, node.CurrentTerm);
        node.CurrentVotesForTerm[node.CurrentTerm].Should().Be(2);

        await node.ResponseVoteRPC(true, node.CurrentTerm);
        node.CurrentVotesForTerm[node.CurrentTerm].Should().Be(3);

        node.CurrentState.Should().Be(NodeState.Leader);
    }

    //END OF PART 1

    // Testing 15
    // Internal Count 11
    [Fact]
    public async Task GivenANodeRecievesASecondVoteForATermInTheFutureThenItShouldAlsoVoteForThatOne()
    {
        var moqNode = Substitute.For<INode>();
        moqNode.Id = 2;   
        Node node = new Node(1, [moqNode]);

        await node.RequestVoteRPC(2, 1);
        await node.RequestVoteRPC(2, 2);

        await moqNode.Received().ResponseVoteRPC(true, 1);
        await moqNode.Received().ResponseVoteRPC(true, 2);
    }

    // Testing 7
    // Internal Count 12.a
    [Fact]
    public async Task GivenAFollowerNodeWhenItRecievesHeartbeatsEvery50msFor300msItDoesNotBecomeACandidate()
    {
        var node = new Node(1);
    
        for (int i = 0; i < 6; i ++)
        {
            Thread.Sleep(50);
            await node.RequestAppendLogRPC(2, 1, []);
        }
        node.CurrentState.Should().Be(NodeState.Follower);
    }

    // Internal Count 12.b
    [Fact]
    public async Task GivenAFollowerNodeWhenItRecievesAHeartbeatEvery50msItChangesTheDurationOfItsTimer()
    {
        var node = new Node(1);

        var exactSameCount = 0;
        for (int i = 0; i < 10; i ++)
        {
            var currentInterval = node.internalTimer?.Interval;
            Thread.Sleep(50);
            await node.RequestAppendLogRPC(2,1, []);
            if(currentInterval == node.internalTimer?.Interval)
            {
                exactSameCount++;
            }
        }

        exactSameCount.Should().BeLessThan(3);
    }

    // Testing #2
    // Internal Count 13.a
    [Fact]
    public async Task GivenAFollowerWhenTheyRecieveAHeartbeatFromANewValidLeaderTheyRememberWhoTheLeaderIs()
    {
        var node = new Node();

        await node.RequestAppendLogRPC(2, 1, []);

        node.CurrentLeader.Should().Be(2);
    }

    // Internal Count 13.b
    [Fact]
    public async Task GivenAFollowerWhenTheyRecieveHeartbeatsFromTwoDIfferentLeadersTheyStoreTheFirstLeaderAsTheCurrentAndNotTheLaterOne()
    {
        var node = new Node();

        await node.RequestAppendLogRPC(2, 1, []);
        await node.RequestAppendLogRPC(3, 1, []);

        node.CurrentLeader.Should().Be(2);
    }


    // Internal Count 13.c
    [Fact]
    public async Task GivenAFollowerNodeWhenTheyRecieveAVoteRequestForATermGreaterThanTheCurrentOneTheCurrentTermChanges()
    {
        var node = new Node();

        await node.RequestAppendLogRPC(2, 1, []);

        node.CurrentTerm.Should().Be(1);
    }

    // Internal Count 13.d
    [Fact]
    public async Task GivenAFollowerNodeWhenTheyHaveAlreadyIdentifiedTheLeaderTheyChangeTheLeaderWhenTheyRecieveARequestFromAHigherTerm()
    {
        var node = new Node();

        await node.RequestAppendLogRPC(2, 1, []);

        node.CurrentLeader.Should().Be(2);

        await node.RequestAppendLogRPC(3, 3, []);

        node.CurrentLeader.Should().Be(3);
    }

    // Testing #17
    // Internal Count 14
    [Fact]
    public async Task GivenAFollowerRecievesAnAppendLogRPCTheyRespondToTheServer()
    {
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;

        var node = new Node(1, [moqLeader]);

        await node.RequestAppendLogRPC(2, 1, []);

        await moqLeader.Received().ResponseAppendLogRPC(true);
    }

    // Testing #18
    // Internal Count 15
    [Fact]
    public async Task GivenAFollowerWhenTheyRecieveAAppendLogRPCFromAnOldTermTheyRejectTheResponse()
    {
        var moqLeader = Substitute.For<INode>();
        moqLeader.Id = 2;

        var node = new Node(1, [moqLeader]);

        await node.RequestAppendLogRPC(2, -1, []);

        await moqLeader.Received().ResponseAppendLogRPC(false);
    }

    // Testing #12
    // Internal Count 16
    [Fact]
    public async Task GivenACandidateNodeWhenItRecievesAnAppendEntryFromSomeonFromALaterTermTheyRevertToBeingAFollower()
    {
        var node = new Node(1, LargeCluster);

        node.InitiateCanidacy();

        await node.RequestAppendLogRPC(2, 5, []);

        node.CurrentState.Should().Be(NodeState.Follower);
    }

    // Testing #13
    // Internal Count 17
    [Fact]
    public async Task GivenACandidateNodeWhenItRecievesAnAppendEntryFromSomeoneFromTheCurrentTermTheySHouldBeAFollower()
    {
        var node = new Node(1, LargeCluster);

        node.InitiateCanidacy();

        await node.RequestAppendLogRPC(1, node.CurrentTerm, []);

        node.CurrentState.Should().Be(NodeState.Follower);
    }

    // Testing #19
    // Internal Count 18
    [Fact]
    public async Task WhenANodeBecomesALeaderTheySendOutHeartbeatsToAssertLeadership()
    {
        var moqNode1 = Substitute.For<INode>();
        var moqNode2 = Substitute.For<INode>();
        var node = new Node(1, [moqNode1, moqNode2]);

        node.InitiateCanidacy();
        node.CurrentTerm.Should().Be(1);

        await node.ResponseVoteRPC(true, node.CurrentTerm);
        await node.ResponseVoteRPC(true, node.CurrentTerm);

        node.CurrentTerm.Should().Be(1);
        node.CurrentState.Should().Be(NodeState.Leader);
        await moqNode1.Received().RequestAppendLogRPC(1,node.CurrentTerm, Arg.Any<Log[]>());
        await moqNode2.Received().RequestAppendLogRPC(1,node.CurrentTerm, Arg.Any<Log[]>());
    }

    // Testing #1
    // Internal count 19
    [Fact]
    public void GivenALeaderNodeTheyWillSendOutHeartbeatsEvery50ms()
    {
        var moqNode1 = Substitute.For<INode>();
        var leaderNode = new Node(1, [moqNode1]);

        leaderNode.InitiateLeadership();

        Thread.Sleep(525);

        moqNode1.Received(11).RequestAppendLogRPC(1, leaderNode.CurrentTerm, Arg.Any<Log[]>());
    }
}