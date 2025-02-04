using RaftLib;

public class ClusterNode
{
    public string Url { get; }
    public NodeData Data { get; set; } = new();
    public ClusterNode(string Url)
    {
        this.Url = Url;
    }

    public async Task Refetch()
    {
        var client = new HttpClient();
        // System.Console.WriteLine("Attemting to get data from " + Url);

        Data = (await client.GetFromJsonAsync<NodeData>(this.Url + "/nodeData")) ?? throw new Exception();
        System.Console.WriteLine(Data);
    }

    public void SendCommand(string key, string value)
    {
        var client = new HttpClient();

        var clientRequest = new ClientRequestDto();
        clientRequest.Key = key;
        clientRequest.Value = value;
        clientRequest.Url = "http://client:8080";
        client.PostAsJsonAsync(Url + "/request/clientrequest", clientRequest);
    }

}