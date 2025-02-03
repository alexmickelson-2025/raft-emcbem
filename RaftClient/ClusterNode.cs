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

}