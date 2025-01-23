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

     public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            Log otherLog = (Log)obj;

            return Term == otherLog.Term &&
                   Key == otherLog.Key &&
                   Value == otherLog.Value;
        }
        
    //Chat made this
    public override int GetHashCode()
    {
        unchecked // Allow overflow for better performance
        {
            int hash = 17;
            hash = hash * 31 + Term.GetHashCode();
            hash = hash * 31 + (Key?.GetHashCode() ?? 0);
            hash = hash * 31 + (Value?.GetHashCode() ?? 0);
            return hash;
        }
    }

}