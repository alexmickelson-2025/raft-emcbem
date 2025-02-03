using Microsoft.AspNetCore.Mvc;
using RaftLib;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");


builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();



app.UseHttpsRedirection();

var ourId = int.Parse(builder.Configuration["NODE_ID"] ?? throw new NotImplementedException("Bwah"));
var clusterSize = int.Parse(builder.Configuration["CLUSTER_SIZE"] ?? throw new NotImplementedException("BWAH2"));
List<RestClientNode> clusterNodes = Enumerable.Range(1, clusterSize ).Except([ourId]).Select(x =>
{
    System.Console.WriteLine($"Making a client for id {x}");
    return new RestClientNode(x);
}).ToList();

var n = new Node(ourId, [.. clusterNodes]);
n.MaxInterval = 300 * 20;
n.MinInterval = 150 * 20;
n.HeartbeatInterval = 2000;

n.Start();

app.MapGet("/nodeData", () =>
{
    System.Console.WriteLine("Someone wants my data");
  return new NodeData(){
    Id = n.Id,
    State = n.CurrentState,
    TimerPercentage = n.GetRemainingTimePercentage(),
    Term = n.CurrentTerm,
    CurrentLeader = n.CurrentLeader,
    CommitIndex = n.InternalCommitIndex,
    Logs = n.LogList,
    StateMachine = n.InternalStateMachine
  };
});

app.MapPost("/request/appendentries", async ([FromBody] RequestAppendLogDto requestDto) =>
{
    System.Console.WriteLine("Recieving an append entries");
    await n.RequestAppendLogRPC(requestDto);
});

app.MapPost("/request/vote", async ([FromBody] RequestVoteDto requestDto) =>
{
    System.Console.WriteLine("Recieving a vote");
    await n.RequestVoteRPC(requestDto);
});

app.MapPost("/response/appendentries", async ([FromBody] ResponseAppendLogDto requestDto) =>
{
    System.Console.WriteLine("responding to an append entries");
    await n.ResponseAppendLogRPC(requestDto);
});

app.MapPost("/response/vote", async ([FromBody] ResponseVoteDto requestDto) =>
{
    System.Console.WriteLine("responding to a vote");
    await n.ResponseVoteRPC(requestDto);
});

app.MapPost("/request/clientrequest", async ([FromBody] ClientRequestDto request) => 
{
    var client = new ClusterClient(request.Url);
    n.ReceiveClientRequest(client, request.Key, request.Value);
});

app.Run();

