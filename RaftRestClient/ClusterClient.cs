using RaftLib;

class ClusterClient : IClient
{
    public string Url { get; set; }
    public ClusterClient(string Url)
    {
        this.Url = Url;
    }
    public async Task ResponseClientRequestRPC(bool isSuccess, string message)
    {
        var client = new HttpClient();
        await client.PostAsync(Url + $"/response/clientrequest/{message}", new StringContent(""));
    }
}
