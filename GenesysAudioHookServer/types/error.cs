namespace GenesysAudioHookServer.Types;

public class ErrorMessage
{
    public string? id { get; set; }
    public string? type { get; set; }
    public int seq { get; set; }
    public int serverseq { get; set; }
    public string? position { get; set; }
    public ErrorParameters? parameters { get; set; }
}

public class ErrorParameters
{
    public int code { get; set; }
    public string? message { get; set; }
    public string? retryAfter { get; set; }
}