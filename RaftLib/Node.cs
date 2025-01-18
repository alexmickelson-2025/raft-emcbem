namespace RaftLib;


public class Node : INode
{
    public NodeState CurrentState { get; set; } = NodeState.Follower;
    public int CurrentTerm { get; set; } = 0;
    public System.Timers.Timer? internalTimer { get; set; }
    public Dictionary<int, int> WhoDidIVoteFor { get; set; } = new();
    public Dictionary<int, int> CurrentVotesForTerm { get; set; } = new();
    public int Id { get; set; }
    public INode[] nodes { get; set; } = [];
    public int MajorityVotesNeeded { get => (nodes.Count() / 2) + 1; }
    public int CurrentLeader { get; set; }
    public DateTime StartTime;
    public double TimerInterval = 0;

    public int MinInterval { get; set; } = 150;
    public int MaxInterval { get; set; } = 301;
    public int HeartbeatInterval { get; set; } = 50;


    public Node(int idNum)
    {
        StartNewCanidacyTimer();
        Id = idNum;
    }

    public Node(int idNum, int minInterval, int maxInterval, int heartbeatInterval) : this(idNum)
    {
        MinInterval = minInterval;
        MaxInterval = maxInterval;
        HeartbeatInterval = heartbeatInterval;
    }

    public Node() : this(0) { }
    public Node(int id, INode[] nodes) : this(id)
    {
        this.nodes = nodes;
    }

    private void StartNewCanidacyTimer()
    {
        internalTimer?.Stop();
        internalTimer?.Dispose();
        TimerInterval = Random.Shared.Next(MinInterval, MaxInterval);
        StartTime = DateTime.Now;
        internalTimer = new System.Timers.Timer(TimerInterval);
        internalTimer.Elapsed += (s, e) => { InitiateCanidacy(); };
        internalTimer.AutoReset = false;
        internalTimer.Start();
    }

    public void InitiateCanidacy()
    {
        internalTimer?.Stop();
        internalTimer?.Dispose();
        CurrentState = NodeState.Candidate;
        CurrentTerm++;
        WhoDidIVoteFor.Add(CurrentTerm, Id);
        CurrentVotesForTerm.Add(CurrentTerm, 1);
        GatherVotes();
    }

    private void GatherVotes()
    {
        StartNewCanidacyTimer();
        SendVotes();
        if (MajorityVotesNeeded == 1)
        {
            InitiateLeadership();
        }
    }

    public void InitiateLeadership()
    {
        internalTimer?.Stop();
        internalTimer?.Dispose();
        CurrentState = NodeState.Leader;
        CurrentLeader = Id;
        SendHeartbeats();
    }

    private void StartHeartbeatTimer()
    {
        internalTimer?.Stop();
        internalTimer?.Dispose();
        TimerInterval = HeartbeatInterval;
        internalTimer = new System.Timers.Timer(HeartbeatInterval);
        internalTimer.Elapsed += (s, e) => { SendHeartbeats(); };
        internalTimer.AutoReset = false;
        internalTimer.Start();
    }

    private void SendHeartbeats()
    {
        StartTime = DateTime.Now;
        foreach (var node in nodes)
        {
            node.RequestAppendLogRPC(Id, CurrentTerm);
        }
        StartHeartbeatTimer();
    }

    private void SendVotes()
    {
        foreach (var node in nodes)
        {
            node.RequestVoteRPC(Id, CurrentTerm);
        }
    }

    public async Task RequestVoteRPC(int candidateId, int termToVoteFor)
    {
        bool result = false;
        if (!WhoDidIVoteFor.TryGetValue(termToVoteFor, out _) && termToVoteFor > CurrentTerm)
        {
            WhoDidIVoteFor.Add(termToVoteFor, candidateId);
            result = true;
        }
        await SendVote(candidateId, result, termToVoteFor);
    }

    public async Task ResponseVoteRPC(bool result, int termToVoteFor)
    {
        await Task.CompletedTask;
        if (result == false) return;
        CurrentVotesForTerm[termToVoteFor]++;
        if (CurrentState == NodeState.Candidate && termToVoteFor == CurrentTerm)
        {
            if (CurrentVotesForTerm[termToVoteFor] >= MajorityVotesNeeded)
            {
                InitiateLeadership();
            }
        }
    }

    public async Task RequestAppendLogRPC(int leaderId, int term)
    {
        if (term >= CurrentTerm)
        {
            if (term > CurrentTerm)
            {
                CurrentTerm = term;
                CurrentLeader = leaderId;
                CurrentState = NodeState.Follower;
            }

            if (term == CurrentTerm && NodeState.Candidate == CurrentState)
            {
                CurrentState = NodeState.Follower;
                CurrentLeader = leaderId;
            }

            if (NodeState.Leader != CurrentState)
            {
                StartNewCanidacyTimer();
            }
        }
        await SendAppendResponse(leaderId, term);
    }

    private async Task SendAppendResponse(int leaderId, int term)
    {
        var nodeToRespondTo = nodes.Where(x => x.Id == leaderId).FirstOrDefault();

        if (nodeToRespondTo != null)
        {
            if (term < CurrentTerm)
            {
                await nodeToRespondTo.ResponseAppendLogRPC(false);
                return;
            }
            await nodeToRespondTo.ResponseAppendLogRPC(true);
        }
    }

    public async Task SendVote(int candidateId, bool result, int termToVoteFor)
    {
        var nodeToCastVoteTo = nodes.Where(x => x.Id == candidateId).FirstOrDefault();

        if (nodeToCastVoteTo != null)
        {
            await nodeToCastVoteTo.ResponseVoteRPC(result, termToVoteFor);
        }
    }

    public async Task ResponseAppendLogRPC(bool ableToSync)
    {
        await Task.CompletedTask;
    }

    public double GetRemainingTime()
    {
        double elapsedTime = (DateTime.Now - StartTime).TotalMilliseconds;
        double remainingTime = TimerInterval - elapsedTime;
        return Math.Max(remainingTime, 0);
    }
}
