using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.VisualBasic;
using NSubstitute;
using RaftLib;

namespace RaftTests;

internal class TestNode : INode
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
}

public class UnitTest1
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

        Thread.Sleep(301);

        node.CurrentState.Should().Be(NodeState.Candidate);
    }

    // Testing #6
    // Internal Count 3
    [Fact]
    public void GivenAFollowerBecomesANewCandidateThenTheInternalTermCounterGoesUpForEachCanidacyThatHappens()
    {
        Node node = new Node();

        Thread.Sleep(302);

        node.CurrentTerm.Should().BeGreaterThan(0);
    }

    // Testing #11
    // Internal Count 4
    [Fact]
    public void GivenANodeJustTurnedToACandidateThenItShouldVoteForItselfForThatTerm()
    {
        Node node = new Node(1);

        Thread.Sleep(301);

        node.WhoDidIVoteFor[1].Should().Be(1);
    }

    // Testing #16
    // Internal Count 5
    [Fact]
    public void GivenACanidateNodeWhenTheElectionTimerEndsAnotherElectionBegins()
    {
        Node node = new Node(1, LargeCluster);

        Thread.Sleep(301);
        var firstWaitedTerm = node.CurrentTerm;
        
        Thread.Sleep(301);
        firstWaitedTerm.Should().BeLessThan(node.CurrentTerm);
        node.WhoDidIVoteFor[node.CurrentTerm].Should().Be(1);
    }

    // Testing #8
    // Internal Count 6.a
    [Fact]
    public void GivenACandidateHasAMajorityOfVotesThenTheyBecomeALeader()
    {
        Node node = new Node(1);

        Thread.Sleep(301);

        node.CurrentState.Should().Be(NodeState.Leader);
    }

    // Internal Count 6.b
    [Fact]
    public async Task GivenACandidateHasToGet3VotesForMajorityAndTheyGet3VotesTheyBecomeLeader()
    {
        Node node = new Node(1, LargeCluster);


        Thread.Sleep(301);

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

            Thread.Sleep(301);
            if(originalInterval == node.internalTimer?.Interval)
            {
                exactSameCount++;
            } 
        }

        exactSameCount.Should().BeLessThan(3);
    }

    // Testing #10
    // Internal Count 8.a
    [Fact]
    public async Task GivenAFollowerIsRequestedForAVoteThenTheyWillRespondWIthAYesSinceTheyHaventVotedForThisTermYet()
    {
        var node2 = Substitute.For<INode>();
        node2.Id = 2;
        var mockedCluster = new INode[]{node2};

        var node = new Node(1, mockedCluster);

        await node.RequestVoteRPC(2, 1);

        await node2.Received().ResponseVoteRPC(true, 1);
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

        Thread.Sleep(301);

        await node2.Received().RequestVoteRPC(1, node.CurrentTerm);
        await node3.Received().RequestVoteRPC(1, node.CurrentTerm);
    }

    // Testing 9
    // Internal Count 10
    [Fact]
    public async Task GivenACandidateWhenTheyRecieveOnlyAFewVotesAndSomeAreUnresponsiveItWillStillTurnToBeingALeaderIfMajority()
    {
        Node node = new Node(1, LargeCluster);


        Thread.Sleep(301);

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
            Thread.Sleep(75);
            await node.RequestAppendLogRPC(2, 1);
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
            await node.RequestAppendLogRPC(2,1);
            if(currentInterval == node.internalTimer?.Interval)
            {
                exactSameCount++;
            }
        }

        exactSameCount.Should().BeLessThan(3);
    }

}