using FluentAssertions;
using RaftLib;

namespace RaftTests;

public class UnitTest1
{
    [Fact]
    public void GivenANodeWhenItHasJustBeenMadeThenItShouldBeAFollower()
    {
        Node node = new Node();

        node.CurrentState.Should().Be(NodeState.Follower);
    }
}