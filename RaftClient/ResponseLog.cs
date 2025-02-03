namespace RaftClient;

public class ResponseLog
{
    public List<string> Responses { get; set; } = new();
    public event Action? ResponsesUpdated;

    public void AddLog(string log)
    {
        Responses.Add(log);
        ResponsesUpdated?.Invoke();
    }
}