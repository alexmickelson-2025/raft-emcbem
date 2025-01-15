

namespace RaftLib;



public class Node
{
    public NodeState CurrentState { get; set; } = NodeState.Follower;
    public int CurrentTerm { get; set; } = 0;
    public System.Timers.Timer? internalTimer { get; set; }
    public Dictionary<int, int> WhoDidIVoteFor {get; set;} = new();
    public Dictionary<int, int> CurrentVotesForTerm {get; set;} = new();
    public int IdNum { get; set; }
    public INode[] nodes{ get; set; } = [];
    public int MajorityVotesNeeded { get => (nodes.Count() / 2) + 1; }
    public int CurrentLeader { get; set; }
    
    public Node(int idNum)
    {
        StartNewCanidacyTimer();
        IdNum = idNum;
    }

    private void StartNewCanidacyTimer()
    {
        internalTimer = new System.Timers.Timer(Random.Shared.Next(150, 301));
        internalTimer.Elapsed += (s, e) => {InitiateCanidacy();};
        internalTimer.AutoReset = false;
        internalTimer.Start();
    }

    public Node() : this(0) {}

    public Node(int id, INode[] nodes) : this(id) 
    {
        this.nodes = nodes;
    }
    
    private void InitiateCanidacy()
    {
        CurrentState = NodeState.Candidate;
        CurrentTerm++;
        WhoDidIVoteFor.Add(CurrentTerm, IdNum);
        CurrentVotesForTerm.Add(CurrentTerm, 1);
        GatherVotes();
    }

    private void GatherVotes()
    {
        var termToCountFor = CurrentTerm;
        StartNewCanidacyTimer();
        SendVotes();
        while(CurrentState == NodeState.Candidate && termToCountFor == CurrentTerm)
        {
            if(CurrentVotesForTerm[termToCountFor] >= MajorityVotesNeeded)
            {
                CurrentState = NodeState.Leader;
            }
        }
    }

    private void SendVotes()
    {
        foreach(var node in nodes)
        {
            node.RequestVoteRPC(IdNum, CurrentTerm);
        }
    }

    public async Task RequestVoteRPC(int candidateId, int termToVoteFor)
    {
        bool result = false;
        if(!WhoDidIVoteFor.TryGetValue(termToVoteFor, out _) && termToVoteFor > CurrentTerm)
        {
            WhoDidIVoteFor.Add(termToVoteFor, candidateId);
            result = true;
        }
        await SendVote(candidateId, result, termToVoteFor);
    }

    public async Task ResponseVoteRPC(bool result, int termToVoteFor)
    {
        await Task.CompletedTask;
        if(result == false) return;
        CurrentVotesForTerm[termToVoteFor]++;
    }

    public async Task RequestAppendLogRPC(int candidateId, int termToVoteFor)
    {
        if (termToVoteFor > CurrentTerm)
        {
            CurrentTerm = termToVoteFor;
            CurrentLeader = candidateId;
        }
        ResetTimer();
    }

    private void ResetTimer()
    {
        if(internalTimer != null)
        {
            internalTimer.Stop();
            internalTimer.Interval = (double)Random.Shared.Next(150, 301);
            internalTimer.Start();
        }
    }

    public async Task SendVote(int candidateId, bool result, int termToVoteFor)
    {
        var nodeToCastVoteTo = nodes.Where(x => x.Id == candidateId).FirstOrDefault();

        if(nodeToCastVoteTo != null)
        {
            await nodeToCastVoteTo.ResponseVoteRPC(result, termToVoteFor);
        }
    }
}
