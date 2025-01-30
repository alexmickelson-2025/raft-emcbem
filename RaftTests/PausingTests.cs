using System.Runtime.InteropServices;
using FluentAssertions;
using NSubstitute;
using RaftTests;

namespace RaftLib;

public class PausingTests
{
    [Fact]
    public void GivenALeaderNodeWhenTheyGetPausedThenTheyDoNotSendHeartbeatsFor400ms()
    {
        // Given
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 0;
        var node = new Node(1, [moqFollower]);
        node.InitiateLeadership();
        node.StopTimer();
    
        // When
        Thread.Sleep(400);
    
        // Then
        moqFollower.Received(1).RequestAppendLogRPC(Arg.Any<RequestAppendLogDto>());
    }

    [Fact]
    public void GivemALeaderNodeWhenTheyGetPausedThenResumeThenTheHeartbeatsShouldStartAgain()
    {
        // Given
        var moqFollower = Substitute.For<INode>();
        moqFollower.Id = 0;
        var node = new Node(1, [moqFollower]);
         node.InitiateLeadership();
        node.StopTimer();
        Thread.Sleep(400);
        moqFollower.Received(1).RequestAppendLogRPC(Arg.Any<RequestAppendLogDto>());
    
        // When
        node.Start();
        Thread.Sleep(415);

        // Then
        moqFollower.Received(9).RequestAppendLogRPC(Arg.Any<RequestAppendLogDto>());
    }

    [Fact]
    public void GivenAFollowerNodeWhenTheyGetPausedThenTheyDoNotBecomeACandidate()
    {
        // Given
        var node = new Node(1);
        node.StopTimer();
    
        // When
        Thread.Sleep(600);
    
        // Then
        node.CurrentState.Should().Be(NodeState.Follower);
    }

    [Fact]
    public void GivenAFollowerNodeWhenTheyGetPausedThenGetUnpausedTheyDoBecomeACandidate()
    {
        // Given
        var node = new Node(1, TestNode.LargeCluster);
        node.StopTimer();
        Thread.Sleep(600);
        node.CurrentState.Should().Be(NodeState.Follower);

        node.Start();
        Thread.Sleep(350);

        node.CurrentState.Should().Be(NodeState.Candidate);
    }
}