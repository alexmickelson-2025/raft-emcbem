using System.Formats.Asn1;

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
    public Dictionary<string, string> InternalStateMachine { get; set; } = new();
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
    public int InternalCommitIndex { get; set; }
    private Action<(string, string, int)>? LogCommitedEvent;



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
            int indexOfPersonalPrevLog =  OtherNextIndexes[node.Id] == 0 ? 0 : OtherNextIndexes[node.Id] ;
            int prevTerm = indexOfPersonalPrevLog == 0 ? 0 : LogList.ElementAtOrDefault(indexOfPersonalPrevLog - 1)?.Term ?? 0;
            //System.Console.WriteLine($"Sending a request to {node.Id}. Commit Index: {InternalCommitIndex}. Index Of PerosnalPrevLog: {indexOfPersonalPrevLog}. Sending over a list of size {GetOtherNodesLogList(node.Id).Count()}. PrevTerm {prevTerm}");

            node.RequestAppendLogRPC(new (Id, CurrentTerm, GetOtherNodesLogList(node.Id), InternalCommitIndex, indexOfPersonalPrevLog, prevTerm));
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

        return LogList.Skip(nodesNextIndex ).Take(logDifference).ToArray();
    }

    private void SendVotes()
    {
        foreach (var node in nodes)
        {
            node.RequestVoteRPC(new (Id, CurrentTerm, InternalCommitIndex));
        }
    }

    private async Task SendAppendResponse(int leaderId, bool response)
    {
        var nodeToRespondTo = nodes.Where(x => x.Id == leaderId).FirstOrDefault();

        if (nodeToRespondTo != null)
        {
            //System.Console.WriteLine($"Sending a response {response} to leader. Next Index: {NextIndex}. CurrentTerm: {CurrentTerm}. My log count is: {LogList.Count()}");

            await nodeToRespondTo.ResponseAppendLogRPC(new (response, Id, CurrentTerm, NextIndex));
        }
    }

    public async Task SendVote(int candidateId, bool result, int termToVoteFor)
    {
        var nodeToCastVoteTo = nodes.Where(x => x.Id == candidateId).FirstOrDefault();

        if (nodeToCastVoteTo != null)
        {
            await nodeToCastVoteTo.ResponseVoteRPC(new (result, termToVoteFor));
        }
    }


    public void ReceiveClientRequest(IClient clientRequesting, string key, string value)
    {
        if(CurrentState != NodeState.Leader)
        {
            clientRequesting.ResponseClientRequestRPC(false, $"Node {Id} is not the leader");
            return;
        }
        LogReplicated.Add(NextIndex, 1);
        RegisterForLogCommitEvent(clientRequesting, key, value, NextIndex);
        LogList.Add(new Log(CurrentTerm, key, value));
        if (Majority == 1)
        {
            LeaderCommitLog(LogList.Count() - 1);
        }
    }

    private void RegisterForLogCommitEvent(IClient clientRequesting, string key, string value, int indexWhenRegistered)
    {
        //Chat made this but I love it
        // Named event handler for easy unsubscription
        void LogHandler((string key, string value, int index) command)
        {
            if (command == (key, value, indexWhenRegistered))
            {
                clientRequesting.ResponseClientRequestRPC(true, $"Leader {Id} was able to get the command key: {key}, value: {value} committed"); // Respond to client
                LogCommitedEvent -= LogHandler; // Unsubscribe to prevent further calls
            }
        }

        // Subscribe to the event
        LogCommitedEvent += LogHandler;
    }

    private void LeaderCommitLog(int logIndexToCommit)
    {
        //TODO: change this method a little bit lol
        var logsToAdd = LogList.Skip(InternalCommitIndex ).Take(logIndexToCommit - InternalCommitIndex + 1).ToList();
        for (int i = 0; i < logsToAdd.Count(); i++)
        {
            var log = logsToAdd[i];
            InternalStateMachine[log.Key] = log.Value;
            LogCommitedEvent?.Invoke((log.Key, log.Value, InternalCommitIndex + i ));
        }
        InternalCommitIndex = logIndexToCommit + 1;
    }
    public double GetRemainingTime()
    {
        double elapsedTime = (DateTime.Now - StartTime).TotalMilliseconds;
        double remainingTime = TimerInterval - elapsedTime;
        return Math.Max(remainingTime, 0);
    }

    public double GetRemainingTimePercentage()
    {
        return GetRemainingTime()/TimerInterval;
    }

    private void CommitNeededLogs(int commitIndex)
    {
        if (LogList.ElementAtOrDefault(commitIndex - 1) != null)
        {
            if (commitIndex <= InternalCommitIndex)
            {
                return;
            }
            else
            {
                var logsToCommit = LogList.Skip(InternalCommitIndex ).Take(commitIndex - InternalCommitIndex);
                foreach (var log in logsToCommit)
                {
                    InternalStateMachine[log.Key] = log.Value;
                }
                InternalCommitIndex = commitIndex;
            }
        }
        else
        {
            return;
        }
    }

    private void AddOrRemoveLogs(int leaderId, Log[] entries, int prevIndex, int prevTerm)
    {
        var ourPrevTerm = NextIndex > 0 ? LogList[NextIndex - 1].Term : 0;
        if (NextIndex != prevIndex || prevTerm != ourPrevTerm)
        {
            if (LogList.Count > prevIndex)
            {
                LogList.RemoveRange(prevIndex, LogList.Count - (prevIndex));
            }
        }
        else
        {
            LogList.AddRange(entries);
        }
    }

    private bool DetermineResponse(int term, int prevIndex, int prevTerm)
    {
        if (term < CurrentTerm)
        {
            return false;
        }
        var ourPrevIndex = NextIndex;
        var ourPrevTerm = NextIndex > 0 ? LogList[NextIndex - 1].Term : 0;
        var response = true;

        if (prevIndex != ourPrevIndex || ourPrevTerm != prevTerm)
        {
            response = false;
        }
        return response;
    }

    public void StopTimer()
    {
        InternalTimer?.Stop();
        StartTime = DateTime.MinValue;
    }

    public void Start()
    {
        InternalTimer?.Start();
        StartTime = DateTime.Now;
    }

    public async Task ResponseVoteRPC(ResponseVoteDto responseVoteDto)
    {
        await Task.CompletedTask;
        if (responseVoteDto.result == false) return;
        CurrentVotesForTerm[responseVoteDto.termToVoteFor]++;
        if (CurrentState == NodeState.Candidate && responseVoteDto.termToVoteFor == CurrentTerm)
        {
            if (CurrentVotesForTerm[responseVoteDto.termToVoteFor] == Majority)
            {
                InitiateLeadership();
            }
        }
    }

    public async Task RequestVoteRPC(RequestVoteDto requestVoteDto)
    {
        bool result = false;
        if(InternalCommitIndex > requestVoteDto.commitIndex)
        {
            result = false;
        }
        else if (!WhoDidIVoteFor.ContainsKey(requestVoteDto.termToVoteFor) && requestVoteDto.termToVoteFor > CurrentTerm)
        {
            WhoDidIVoteFor.Add(requestVoteDto.termToVoteFor, requestVoteDto.candidateId);
            result = true;
        }
        await SendVote(requestVoteDto.candidateId, result, requestVoteDto.termToVoteFor);
    }

    public async Task ResponseAppendLogRPC(ResponseAppendLogDto responseAppendLogDto)
    {
        if (responseAppendLogDto.term < CurrentTerm)
        {
            return;
        }
        if (responseAppendLogDto.ableToSync)
        {
            OtherNextIndexes[responseAppendLogDto.id] = responseAppendLogDto.indexOfAddedLog;
            int indexLogWasAddedToLast = responseAppendLogDto.indexOfAddedLog - 1;
            if (LogReplicated.ContainsKey(indexLogWasAddedToLast))
            {
                LogReplicated[indexLogWasAddedToLast]++;
                if (LogReplicated[indexLogWasAddedToLast] == Majority)
                {
                    LeaderCommitLog(indexLogWasAddedToLast);
                }
            }
        }
        else
        {
            if(OtherNextIndexes[responseAppendLogDto.id] > 0)
            {
                OtherNextIndexes[responseAppendLogDto.id]--;
            }
        }
        await Task.CompletedTask;
    }

    public async Task RequestAppendLogRPC(RequestAppendLogDto requestAppendLogDto)
    {
        if (requestAppendLogDto.term >= CurrentTerm)
        {
            if (requestAppendLogDto.term > CurrentTerm)
            {
                CurrentTerm = requestAppendLogDto.term;
                CurrentLeader = requestAppendLogDto.leaderId;
                CurrentState = NodeState.Follower;
            }

            if (requestAppendLogDto.term == CurrentTerm && NodeState.Candidate == CurrentState)
            {
                CurrentState = NodeState.Follower;
                CurrentLeader = requestAppendLogDto.leaderId;
            }

            if (NodeState.Leader != CurrentState)
            {
                StartNewCanidacyTimer();
            }
        }
        var originalRepsonse = DetermineResponse(requestAppendLogDto.term, requestAppendLogDto.prevIndex, requestAppendLogDto.prevTerm);
        AddOrRemoveLogs(requestAppendLogDto.leaderId, requestAppendLogDto.entries, requestAppendLogDto.prevIndex, requestAppendLogDto.prevTerm);
        CommitNeededLogs(requestAppendLogDto.commitIndex);
        var response = DetermineResponse(requestAppendLogDto.term, requestAppendLogDto.prevIndex, requestAppendLogDto.prevTerm);
        await SendAppendResponse(requestAppendLogDto.leaderId, response || originalRepsonse); }
}
