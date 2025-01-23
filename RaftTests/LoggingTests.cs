using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using RaftLib;

namespace RaftTests;

public class LoggingTests
{
    // Testing #1
    [Fact]
    public async Task GivenALeaderWhenWorkingWithAClientRequestItSendsAnAppendEntriesRPCToFollowers()
    {
        var moqNode = Substitute.For<INode>();
        moqNode.Id = 0;
        var leaderNode = new Node(1, [moqNode]);
        leaderNode.InitiateLeadership();

        leaderNode.ReceiveClientRequest("test1", "test2");
        await Task.Delay(75);

       await moqNode.Received().RequestAppendLogRPC(1, 0, Arg.Is<Log[]>(logs =>
            logs.Length == 1 &&
            logs[0].Equals(new Log(0, "test1", "test2"))
        ));
        }


    // Testing #2
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

        node.NextIndex = 12;
        node.InitiateLeadership();

        node.OtherNextIndexes[2].Should().Be(13);
        node.OtherNextIndexes.Values.All(x => x == 13).Should().BeTrue();
    }
}