namespace RaftLib;

public class Node
{
    public NodeState CurrentState { get; set; } = NodeState.Follower;
}
