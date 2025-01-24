namespace RaftLib;


public class Node : INode
{
    public NodeState CurrentState { get; set; } = NodeState.Follower;
    public int CurrentTerm { get; set; } = 0;
    public System.Timers.Timer? InternalTimer { get; set; }
    public Dictionary<int, int> OtherNextIndexes { get; set; } = new();
    public Dictionary<int, int> WhoDidIVoteFor { get; set; } = new();
    public Dictionary<int, int> CurrentVotesForTerm { get; set; } = new();
    public Dictionary<int, int> LogReplicated { get; set; } = new();
    public Dictionary<string, string> InternalStateMachine {get; set;} = new();
    public int Id { get; set; }
    public INode[] nodes { get; set; } = [];
    public int Majority { get => ((nodes.Count() + 1) / 2) + 1; }
    public int CurrentLeader { get; set; }
    public DateTime StartTime;
    public double TimerInterval = 0;
    public List<Log> LogList { get; set; } = new();
    public int NextIndex { get => LogList.Count; }
    public int MinInterval { get; set; } = 150;
    public int MaxInterval { get; set; } = 301;
    public int HeartbeatInterval { get; set; } = 50;
    public int CommitIndex { get; set; }


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
        InternalTimer?.Stop();
        InternalTimer?.Dispose();
        TimerInterval = Random.Shared.Next(MinInterval, MaxInterval);
        StartTime = DateTime.Now;
        InternalTimer = new System.Timers.Timer(TimerInterval);
        InternalTimer.Elapsed += (s, e) => { InitiateCanidacy(); };
        InternalTimer.AutoReset = false;
        InternalTimer.Start();
    }

    public void InitiateCanidacy()
    {
        InternalTimer?.Stop();
        InternalTimer?.Dispose();
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
        if (Majority == 1)
        {
            InitiateLeadership();
        }
    }

    public void InitiateLeadership()
    {
        InternalTimer?.Stop();
        InternalTimer?.Dispose();

        CurrentState = NodeState.Leader;
        CurrentLeader = Id;

        SetupOtherNodesIndexes();

        StartHeartbeatTimer();
    }

    private void SetupOtherNodesIndexes()
    {
        foreach (var node in nodes)
        {
            OtherNextIndexes[node.Id] = NextIndex + 1;
        }
    }

    private void StartHeartbeatTimer()
    {
        SendHeartbeats();
        InternalTimer?.Stop();
        InternalTimer?.Dispose();
        TimerInterval = HeartbeatInterval;
        StartTime = DateTime.Now;
        InternalTimer = new System.Timers.Timer(HeartbeatInterval);
        InternalTimer.Elapsed += (s, e) => { StartHeartbeatTimer(); };
        InternalTimer.AutoReset = false;
        InternalTimer.Start();
    }

    private void SendHeartbeats()
    {
        foreach (var node in nodes)
        {
            node.RequestAppendLogRPC(Id, CurrentTerm, GetOtherNodesLogList(node.Id), CommitIndex, LogList.Count, LogList.LastOrDefault()?.Term ?? 0);
        }
    }

    private Log[] GetOtherNodesLogList(int nodeId)
    {
        var nodesNextIndex = OtherNextIndexes[nodeId];
        var logDifference = NextIndex + 1 - nodesNextIndex;

        if (logDifference < 0)
        {
            return [];
        }

        return LogList.Skip(nodesNextIndex - 1).Take(logDifference).ToArray();
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
        if (!WhoDidIVoteFor.ContainsKey(termToVoteFor) && termToVoteFor > CurrentTerm)
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
            if (CurrentVotesForTerm[termToVoteFor] >= Majority)
            {
                InitiateLeadership();
            }
        }
    }

    private async Task SendAppendResponse(int leaderId, bool response)
    {
        var nodeToRespondTo = nodes.Where(x => x.Id == leaderId).FirstOrDefault();

        if (nodeToRespondTo != null)
        {
            await nodeToRespondTo.ResponseAppendLogRPC(response, Id, CurrentTerm,  NextIndex);
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

    public async Task ResponseAppendLogRPC(bool ableToSync, int id, int term, int index)
    {
        if (term < CurrentTerm)
        {
            return;
        }
        if (ableToSync)
        {
            if (LogReplicated.ContainsKey(index))
            {
                LogReplicated[index]++;
                if (LogReplicated[index] >= Majority)
                {
                    CommitLog(index);
                }
            }
        }
        else
        {
            OtherNextIndexes[id]--;
        }
        await Task.CompletedTask;
    }


    public void ReceiveClientRequest(string key, string value)
    {
        LogReplicated.Add(NextIndex, 1);
        LogList.Add(new Log(CurrentTerm, key, value));
        if (Majority == 1)
        {
            CommitLog(LogList.Count() - 1);
        }
    }

    private void CommitLog(int logIndexToCommit)
    {
        CommitIndex++;
        var LogToAdd = LogList[logIndexToCommit];
        InternalStateMachine[LogToAdd.Key] = LogToAdd.Value;
    }
    public double GetRemainingTime()
    {
        double elapsedTime = (DateTime.Now - StartTime).TotalMilliseconds;
        double remainingTime = TimerInterval - elapsedTime;
        return Math.Max(remainingTime, 0);
    }

    public async Task RequestAppendLogRPC(int leaderId, int term, Log[] entries, int commitIndex, int prevIndex, int prevTerm)
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
        AddOrRemoveLogs(leaderId, entries, commitIndex, prevIndex, prevTerm);
        await SendAppendResponse(leaderId, DetermineResponse(term,commitIndex, prevIndex, prevTerm));
    }

    private void AddOrRemoveLogs(int leaderId, Log[] entries, int commitIndex, int prevIndex, int prevTerm)
    {
        var ourPrevTerm = NextIndex > 0 ? LogList[NextIndex - 1].Term : 0;
        if(NextIndex != prevIndex || prevTerm != ourPrevTerm)
        {
            if (LogList.Count > prevIndex )
            {
                LogList.RemoveRange(prevIndex, LogList.Count - (prevIndex));
            }
        }
        else
        {
            LogList.AddRange(entries);
        }
    }

    private bool DetermineResponse(int term, int commitIndex, int prevIndex, int prevTerm)
    {
        if(term < CurrentTerm)
        {
            return false;
        }
        var ourPrevIndex = NextIndex ;
        var ourPrevTerm = NextIndex > 0 ? LogList[NextIndex - 1].Term : 0;
        var response = true;

        if(prevIndex != ourPrevIndex || ourPrevTerm != prevTerm)
        {
            response =  false;
        }
        return response;
    }

    public void StopTimer()
    {
        InternalTimer?.Dispose();
    }
}
