namespace GenesysAudioHookServer.types;

internal class Media
{
    public string? type { get; set; }
    public string? format { get; set; }
    public List<string>? channels { get; set; }
    public int rate { get; set; }
}
