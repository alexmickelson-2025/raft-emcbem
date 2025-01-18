using RaftLib;

public class SimulationNode : INode
{
    public Node InnerNode { get; }
    public int Id { get => InnerNode.Id; set => InnerNode.Id = value; }

    public int NetworkDelay {get; set;} = 10;
    
    public SimulationNode(Node node)
    {
        InnerNode = node;
    }

    public async Task RequestVoteRPC(int candidateId, int termToVoteFor)
    {
        await Task.Delay(NetworkDelay);
        await InnerNode.RequestVoteRPC(candidateId, termToVoteFor);
    }

    public async Task ResponseAppendLogRPC(bool ableToSync)
    {
        await InnerNode.ResponseAppendLogRPC(ableToSync);
    }

    public async Task ResponseVoteRPC(bool result, int termToVoteFor)
    {
        await InnerNode.ResponseVoteRPC(result, termToVoteFor);
    }

    public async Task RequestAppendLogRPC(int leaderId, int termLogIsFrom)
    {
        await Task.Delay(NetworkDelay);

        await InnerNode.RequestAppendLogRPC(leaderId, termLogIsFrom);
    }
}