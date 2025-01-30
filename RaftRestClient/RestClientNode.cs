using RaftLib;

public class RestClientNode : INode
{
    public int Id { get; set; }

    private string Url {get => $"http://node{Id}:8080";}

    public RestClientNode(int id)
    {
        Id = id;
    }

    public async Task ResponseVoteRPC(ResponseVoteDto responseVoteDto)
    {
        var client = new HttpClient();

        await client.PostAsJsonAsync($"{Url}/response/vote",responseVoteDto);
    }

    public async Task RequestVoteRPC(RequestVoteDto requestVoteDto)
    {
        var client = new HttpClient();

        await client.PostAsJsonAsync($"{Url}/request/vote",requestVoteDto);
    }

    public async Task ResponseAppendLogRPC(ResponseAppendLogDto responseAppendLogDto)
    {
        var client = new HttpClient();

        await client.PostAsJsonAsync($"{Url}/response/appendentries",responseAppendLogDto);
    }

    public async Task RequestAppendLogRPC(RequestAppendLogDto requestAppendLogDto)
    {
        var client = new HttpClient();

        await client.PostAsJsonAsync($"{Url}/request/appendentries",requestAppendLogDto);
    }
}