namespace RaftLib;

public interface INode
{
    public int Id { get; set; }
    public Task ResponseVoteRPC(ResponseVoteDto responseVoteDto);
    public Task RequestVoteRPC(RequestVoteDto requestVoteDto);
    public Task ResponseAppendLogRPC(ResponseAppendLogDto responseAppendLogDto);
    public Task RequestAppendLogRPC(RequestAppendLogDto requestAppendLogDto);
}