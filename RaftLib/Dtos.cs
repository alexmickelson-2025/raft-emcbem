namespace RaftLib;

public record ResponseVoteDto
{
    public bool result { get; set; }
    public int termToVoteFor { get; set; }
    public ResponseVoteDto(bool result, int termToVoteFor)
    {
        this.result = result;
        this.termToVoteFor = termToVoteFor;
    }
}

public record RequestVoteDto
{
    public int candidateId { get; set; }
    public int termToVoteFor { get; set; }
    public int commitIndex { get; set; }

    public RequestVoteDto(int candidateId, int termToVoteFor, int commitIndex)
    {
        this.candidateId = candidateId;
        this.termToVoteFor = termToVoteFor;
        this.commitIndex = commitIndex;
    }
}

public record ResponseAppendLogDto
{
    public bool ableToSync { get; set; }
    public int id { get; set; }
    public int term { get; set; }
    public int indexOfAddedLog { get; set; }

    public ResponseAppendLogDto(bool ableToSync, int id, int term, int indexOfAddedLog)
    {
        this.ableToSync = ableToSync;
        this.id = id;
        this.term = term;
        this.indexOfAddedLog = indexOfAddedLog;
    }
}

public record RequestAppendLogDto
{
    public int leaderId { get; set; }
    public int term { get; set; }
    public Log[] entries { get; set; } = [];
    public int commitIndex { get; set; }
    public int prevIndex { get; set; }
    public int prevTerm { get; set; }
    
    public RequestAppendLogDto(int leaderId, int term, Log[] entries, int commitIndex, int prevIndex, int prevTerm)
    {
        this.leaderId = leaderId;
        this.term = term;
        this.entries = entries;
        this.commitIndex = commitIndex;
        this.prevIndex = prevIndex;
        this.prevTerm = prevTerm;
    }
}
