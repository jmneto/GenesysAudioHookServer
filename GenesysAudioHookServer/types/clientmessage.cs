namespace GenesysAudioHookServer.Types;

internal class Clientmessage : Messageheader
{
    public int serverseq { get; set; }
    public string? position { get; set; }
}