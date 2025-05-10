namespace GenesysAudioHookServer.Types;

public enum AzureGenesysEvent
{
    session_started,
    session_ended,
    recording_available,
    transcript_available,
    partial_transcript
}

public enum ServerMessageType
{
    closed,
    disconnect,
    @event, // `event` is a reserved keyword, so it is prefixed with `@`
    opened,
    pause,
    pong,
    reconnect,
    resume,
    updated
}

public enum ClientMessageType
{
    close,
    closed,
    discarded,
    dtmf,
    error,
    open,
    paused,
    ping,
    playback_completed,
    playback_started,
    resumed,
    update
}

public enum CloseReason
{
    disconnect,
    end,
    error,
    reconnect
}

public enum DisconnectReason
{
    completed,
    error,
    unauthorized
}

public enum MediaFormat
{
    PCMU,
    L16
}