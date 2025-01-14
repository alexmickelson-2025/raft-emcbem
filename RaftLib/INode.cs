namespace RaftLib;

public interface INode
{
    public int Id { get; set; }

    public Task ResponseVoteRPC(bool result, int termToVoteFor);
    public Task RequestVoteRPC(int candidateId, int termToVoteFor);
}