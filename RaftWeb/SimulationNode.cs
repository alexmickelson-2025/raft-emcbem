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

    public async Task ResponseVoteRPC(ResponseVoteDto responseVoteDto)
    {
        Thread.Sleep(NetworkDelay);
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        await InnerNode.ResponseVoteRPC(responseVoteDto);
    }

    public async Task RequestVoteRPC(RequestVoteDto requestVoteDto)
    {
        Thread.Sleep(NetworkDelay);
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        await InnerNode.RequestVoteRPC(requestVoteDto);
    }

    public async Task ResponseAppendLogRPC(ResponseAppendLogDto responseAppendLogDto)
    {
        Thread.Sleep(NetworkDelay);
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        await InnerNode.ResponseAppendLogRPC(responseAppendLogDto);
    }

    public async Task RequestAppendLogRPC(RequestAppendLogDto requestAppendLogDto)
    {
        Thread.Sleep(NetworkDelay);
        if(IsStopped)
        {
            await Task.CompletedTask;
            return;
        }
        await InnerNode.RequestAppendLogRPC(requestAppendLogDto);
    }
}