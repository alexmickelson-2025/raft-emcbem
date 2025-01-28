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
    private Action<(string, string)>? LogCommitedEvent;



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
            System.Console.WriteLine($"Sending a request to {node.Id}. Commit Index: {InternalCommitIndex}. Index Of PerosnalPrevLog: {indexOfPersonalPrevLog}. Sending over a list of size {GetOtherNodesLogList(node.Id).Count()}. PrevTerm {prevTerm}");

            node.RequestAppendLogRPC(Id, CurrentTerm, GetOtherNodesLogList(node.Id), InternalCommitIndex, indexOfPersonalPrevLog, prevTerm);
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
            node.RequestVoteRPC(Id, CurrentTerm, InternalCommitIndex);
        }
    }

    public async Task RequestVoteRPC(int candidateId, int termToVoteFor, int committIndex)
    {
        bool result = false;
        if(InternalCommitIndex > committIndex)
        {
            result = false;
        }
        else if (!WhoDidIVoteFor.ContainsKey(termToVoteFor) && termToVoteFor > CurrentTerm)
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
            if (CurrentVotesForTerm[termToVoteFor] == Majority)
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
            System.Console.WriteLine($"Sending a response {response} to leader. Next Index: {NextIndex}. CurrentTerm: {CurrentTerm}. My log count is: {LogList.Count()}");

            await nodeToRespondTo.ResponseAppendLogRPC(response, Id, CurrentTerm, NextIndex);
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

    public async Task ResponseAppendLogRPC(bool ableToSync, int id, int term, int othersNextIndex)
    {
        Console.WriteLine($"Recieved a response of {ableToSync}, id: {id}, term: {term}, nextIndex {othersNextIndex} \n");
        if (term < CurrentTerm)
        {
            return;
        }
        if (ableToSync)
        {
            OtherNextIndexes[id] = othersNextIndex;
            int indexLogWasAddedToLast = othersNextIndex - 1;
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
            if(OtherNextIndexes[id] > 0)
            {
                OtherNextIndexes[id]--;
            }
        }
        await Task.CompletedTask;
    }


    public void ReceiveClientRequest(IClient clientRequesting, string key, string value)
    {
        if(CurrentState != NodeState.Leader)
        {
            clientRequesting.ResponseClientRequestRPC(false);
            return;
        }
        LogReplicated.Add(NextIndex, 1);
        LogList.Add(new Log(CurrentTerm, key, value));
        RegisterForLogCommitEvent(clientRequesting, key, value);
        if (Majority == 1)
        {
            LeaderCommitLog(LogList.Count() - 1);
        }
    }

    private void RegisterForLogCommitEvent(IClient clientRequesting, string key, string value)
    {
        //Chat made this but I love it
        // Named event handler for easy unsubscription
        void LogHandler((string key, string value) command)
        {
            if (command == (key, value))
            {
                clientRequesting.ResponseClientRequestRPC(true); // Respond to client
                LogCommitedEvent -= LogHandler; // Unsubscribe to prevent further calls
            }
        }

        // Subscribe to the event
        LogCommitedEvent += LogHandler;
    }

    private void LeaderCommitLog(int logIndexToCommit)
    {
        InternalCommitIndex++;
        var LogToAdd = LogList[logIndexToCommit];
        InternalStateMachine[LogToAdd.Key] = LogToAdd.Value;
        LogCommitedEvent?.Invoke((LogToAdd.Key, LogToAdd.Value));
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
        var originalRepsonse = DetermineResponse(term, prevIndex, prevTerm);
        AddOrRemoveLogs(leaderId, entries, prevIndex, prevTerm);
        CommitNeededLogs(commitIndex);
        var response = DetermineResponse(term, prevIndex, prevTerm);
        await SendAppendResponse(leaderId, response || originalRepsonse);
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
                var logsToCommit = LogList.Skip(InternalCommitIndex).Take(commitIndex - InternalCommitIndex);
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
}
