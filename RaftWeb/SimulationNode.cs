using RaftLib;

public class SimulationNode : INode
{
    public Node InnerNode { get; }
    public int Id { get => InnerNode.Id; set => InnerNode.Id = value; }
    public bool IsStopped { get; set; }

    public int NetworkDelay {get; set;} = 10;
    
    public SimulationNode(Node node)
    {
        InnerNode = node;
    }

    public async Task ResponseVoteRPC(bool result, int termToVoteFor)
    {
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        Thread.Sleep(NetworkDelay);
        await InnerNode.ResponseVoteRPC(result, termToVoteFor);
    }

    public async Task RequestVoteRPC(int candidateId, int termToVoteFor, int commitIndex)
    {
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        Thread.Sleep(NetworkDelay);
        await InnerNode.RequestVoteRPC(candidateId, termToVoteFor, commitIndex);
    }

    public async Task ResponseAppendLogRPC(bool ableToSync, int id, int term, int indexOfAddedLog)
    {
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        Thread.Sleep(NetworkDelay);
        await InnerNode.ResponseAppendLogRPC(ableToSync, id, term, indexOfAddedLog);
    }

    public async Task RequestAppendLogRPC(int leaderId, int term, Log[] entries, int commitIndex, int prevIndex, int prevTerm)
    {
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        Thread.Sleep(NetworkDelay);
        await InnerNode.RequestAppendLogRPC(leaderId, term, entries, commitIndex, prevIndex, prevTerm);
    }
}