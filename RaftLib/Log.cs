namespace RaftLib;

public class Log
{
    public int Term { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";

    public Log(int term, string key, string value)
    {
        Term = term;
        Key = key;
        Value = value;
    }

}