namespace RaftLib;

public interface IClient{
    public Task ResponseClientRequestRPC(bool isSuccess);
}