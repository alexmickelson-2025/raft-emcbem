using System.Drawing;
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
        ), 0);
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

        node.ReceiveClientRequest("key", "log");
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

        moqNode1.Received().RequestAppendLogRPC(0, 0, Arg.Is<Log[]>(logs => logs.Count() == 2 && logs[0].Equals(log1) && logs[1].Equals(log2) ), 0);
        moqNode2.Received().RequestAppendLogRPC(0, 0, Arg.Is<Log[]>(logs => logs.Count() == 1 && logs[0].Equals(log2) ), 0);
    }

    // Testing #6 
    [Fact]
    public async Task GivenALeaderWhenSendingOutHeartbeatsThenTheLeaderIncludesTheHighestCommittedEntry()
    {
        var moqNode1 = Substitute.For<INode>();
        moqNode1.Id = 1;
        var node = new Node(1, [moqNode1]);

        //Arrange
        node.InitiateLeadership();
        await moqNode1.Received().RequestAppendLogRPC(1, 0, Arg.Any<Log[]>(), 0);
        node.ReceiveClientRequest("test", "log");
        await node.ResponseAppendLogRPC(true, 1, 0, 1);
        await Task.Delay(75);

        //Assert
        await moqNode1.Received().RequestAppendLogRPC(1, 0, Arg.Any<Log[]>(), 1);
    }

    // Testing #8.a
    [Fact]
    public void GivenALeaderNodeWhenTheyRecieveAClientRequestAndTheyAreTheOnlyNodeInTheClusterItIsCommitedAndTheIndexGoesUp()
    {
        var node =  new Node(1, []);
    
        node.ReceiveClientRequest("hi", "there");    

        node.CommitIndex.Should().Be(1);  
    }

    // Testing #8.b
    [Fact]
    public async Task GivenALeaderNodeWhenTheyRecieveAClientRequestAndTheClusterSizeIs5InThen2NodesRespondWithSuccessThenTheIndexGoesUp()
    {
        var node =  new Node(1, TestNode.LargeCluster);
    
        node.InitiateLeadership();
        node.ReceiveClientRequest("hi", "there");    
        node.CommitIndex.Should().Be(0);  

        await node.ResponseAppendLogRPC(true, 2, 0, 1);
        node.CommitIndex.Should().Be(0);  

        await node.ResponseAppendLogRPC(true, 3, 0, 1);
        node.CommitIndex.Should().Be(1);  
    }
}