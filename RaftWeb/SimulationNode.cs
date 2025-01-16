using RaftLib;

public class SimulationNode : INode
{
    public Node InnerNode { get; }
    public int Id { get => InnerNode.Id; set => InnerNode.Id = value; }
    
    public SimulationNode(Node node)
    {
        InnerNode = node;
    }

    public Task RequestVoteRPC(int candidateId, int termToVoteFor)
    {
        return InnerNode.RequestVoteRPC(candidateId, termToVoteFor);
    }

    public Task ResponseAppendLogRPC(bool ableToSync)
    {
        return InnerNode.ResponseAppendLogRPC(ableToSync);
    }

    public Task ResponseVoteRPC(bool result, int termToVoteFor)
    {
        return InnerNode.ResponseVoteRPC(result, termToVoteFor);
    }

    public Task RequestAppendLogRPC(int leaderId, int termLogIsFrom)
    {
        return InnerNode.RequestAppendLogRPC(leaderId, termLogIsFrom);
    }
}