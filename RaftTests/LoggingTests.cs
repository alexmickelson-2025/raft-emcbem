using FluentAssertions;
using NSubstitute;
using RaftLib;

namespace RaftTests;

public class LoggingTests
{
    [Fact]
    public void GivenLeaderNodeWhenServicingClientRequestItPutsTheLogAtTheEnd()
    {
        var leaderNode = new Node();
        leaderNode.InitiateLeadership();

        leaderNode.ReceiveClientRequest("test1", "value1");

        leaderNode.LogList[0].Term.Should().Be(0);
        leaderNode.LogList[0].Key.Should().Be("test1");
        leaderNode.LogList[0].Value.Should().Be("value1");

        leaderNode.ReceiveClientRequest("test2", "value2");

        leaderNode.LogList[1].Term.Should().Be(0);
        leaderNode.LogList[1].Key.Should().Be("test2");
        leaderNode.LogList[1].Value.Should().Be("value2");

    }
}