namespace RaftLib;

public interface INode
{
    public int Id { get; set; }

    public Task ResponseVoteRPC(bool result, int termToVoteFor);
    public Task RequestVoteRPC(int candidateId, int termToVoteFor, int commitIndex);
    public Task ResponseAppendLogRPC(bool ableToSync, int id, int term, int indexOfAddedLog);
    public Task RequestAppendLogRPC(int leaderId, int term, Log[] entries, int commitIndex, int prevIndex, int prevTerm);
}