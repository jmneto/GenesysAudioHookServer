namespace GenesysAudioHookServer.Types;

public class LogicalSessionStorage
{
    public string? organizationId { get; set; }
    public string? ConversationId { get; set; }
    public byte[]? RawAudioBuffer { get; set; }
    public string? OpenTransactionJson { get; set; }
}