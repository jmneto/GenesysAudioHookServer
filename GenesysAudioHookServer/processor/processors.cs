using System.Text.Json;

using GenesysAudioHookServer.Helpers;
using GenesysAudioHookServer.Server;
using GenesysAudioHookServer.types;
using GenesysAudioHookServer.Types;

namespace GenesysAudioHookServer.Processors;

internal class ProcessHook : WebSocketManager
{
    // Public Methods
    //////////////

    public ProcessHook(Logger logger, CancellationToken cancellationToken) : base(logger, cancellationToken) { }


    // Protected Methods
    ////////////////////

    protected override async Task HandleIncomingMessageAsync(string sessionId, string message)
    {
        // Retrieve the session
        if (!TryGetSession(sessionId, out var session))
            return;

        LogicalSessionStorage storage = session!.LogicalSessionStorage is LogicalSessionStorage logicalStorage
            ? logicalStorage
            : new LogicalSessionStorage();

        try
        {
            //Parse the incoming message
            Dictionary<string, object>? clientMessage = JsonSerializer.Deserialize<Dictionary<string, object>>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (clientMessage == null || !clientMessage.ContainsKey("id") || !clientMessage.ContainsKey("type") || !clientMessage.ContainsKey("seq"))
            {
                // Log the error and return
                _logger.Log("Invalid message format.", LogLevel.Warning);
                return;
            }

            // Extract the message type, ID and sequence number
            string? messageType = clientMessage["type"].ToString();
            string? id = clientMessage["id"].ToString();
            int clientSeq = Convert.ToInt32(clientMessage["seq"].ToString());  // seq has the client sequence number

            // Validate sequence number
            if (clientSeq != session.ClientSeq + 1)
            {
                await Disconnect(sessionId, DisconnectReason.error);
                return;
            }

            // Update client sequence number
            session.ClientSeq = clientSeq;

            // Save the message to the session
            session.LogicalSessionStorage = storage;

            // Handle message types
            switch (messageType)
            {
                case var type when type == ClientMessageType.open.ToString():
                    await HandleClientOpenMessageAsync(sessionId, message, id, session.ClientSeq, session.ServerSeq);
                    break;

                case var type when type == ClientMessageType.ping.ToString():
                    await HandleClientPingMessageAsync(sessionId, message, id, session.ClientSeq, session.ServerSeq);
                    break;

                case var type when type == ClientMessageType.update.ToString():
                    await HandleClientUpdateMessageAsync(sessionId, message);
                    break;

                case var type when type == ClientMessageType.close.ToString():
                    await HandleClientCloseMessageAsync(sessionId, message, id, session.ClientSeq, session.ServerSeq);
                    break;

                case var type when type == ClientMessageType.error.ToString():
                    await HandleClientErrorMessageAsync(sessionId, message);
                    break;

                default:
                    _logger.Log($"[{sessionId}] Unknown message type: {messageType}", LogLevel.Warning);
                    break;
            }


            await Task.CompletedTask;
        }
        catch (JsonException ex)
        {
            _logger.Log($"Failed to deserialize message: {ex.Message}\n{ex}", LogLevel.Error);
            return;
        }
        catch (Exception ex)
        {
            _logger.Log($"An error occurred while processing the message: {ex.Message}\n{ex}", LogLevel.Error);
            return;
        }
    }

    protected override async Task HandleIncomingBytesAsync(string sessionId, byte[] data)
    {
        _logger.Log($"Processing sessionId: {sessionId}, data length: {data.Length} bytes", LogLevel.Info);
        // Add your byte processing logic here
        await Task.CompletedTask;
    }

    protected async Task Disconnect(string sessionId, DisconnectReason reason)
    {
        // Disconnect logic here
        _logger.Log($"Disconnecting session {sessionId} due to {reason}.", LogLevel.Info);

        // Retrieve the session
        if (!TryGetSession(sessionId, out var session))
            return;

        LogicalSessionStorage storage = session!.LogicalSessionStorage is LogicalSessionStorage logicalStorage
        ? logicalStorage
        : new LogicalSessionStorage();

        JsonObject para = new JsonObject();
        para.Add("reason", reason.ToString());

        Servermessage responseMessage = new Servermessage
        {
            version = "2",
            type = ServerMessageType.disconnect.ToString(),
            seq = session.ServerSeq,
            clientseq = session.ClientSeq,
            id = session.SessionId,
            parameters = para
        };

        var disconnectMessage = JsonSerializer.Serialize(responseMessage);

        await SendMessageAsync(sessionId, disconnectMessage);
    }


    // Private Methods
    ///////////////////


    private async Task HandleClientOpenMessageAsync(string sessionId, string openTransactionJson, string? id, int seq, int serverSeq)
    {
        // Deserialize the message into the OpenMessage type
        OpenMessage? openMessage = JsonSerializer.Deserialize<OpenMessage>(openTransactionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (openMessage == null || openMessage.parameters == null)
        {
            _logger.Log($"[{sessionId}] Invalid or missing 'parameters' in open message.", LogLevel.Warning);
            return;
        }

        JsonObject parameters = openMessage!.parameters;

        // Extract fields from the deserialized object
        string? organizationId = parameters.GetValueOrDefault<string>("organizationId");
        string? conversationId = parameters.GetValueOrDefault<string>("conversationId");
        string? position = openMessage.position;
        string? language = parameters.GetValueOrDefault<string>("language");

        Participant? participant = parameters.GetValueOrDefault<Participant>("participant");
        string? ani = participant?.ani;
        string? aniName = participant?.aniName;
        string? dnis = participant?.dnis;

        Media[]? media = parameters.GetValueOrDefault<Media[]>("media");

        // Handle connection probe
        if (conversationId == "00000000-0000-0000-0000-000000000000")
        {
            await HandleConnectionProbeAsync(sessionId, id, seq);
            return;
        }

        // Select stereo media if available, otherwise fallback to the first media format
        Media? selectedMedia = media?.FirstOrDefault(m =>
            m.channels != null &&
            m.channels.Contains("internal") &&
            m.channels.Contains("external")) ?? media?.FirstOrDefault();

        if (selectedMedia == null)
        {
            _logger.Log($"[{sessionId}] No valid media format found.", LogLevel.Warning);
            return;
        }

        // Store data in _activeSessions
        if (TryGetSession(sessionId, out var session))
        {
            LogicalSessionStorage storage = session!.LogicalSessionStorage is LogicalSessionStorage logicalStorage
                ? logicalStorage
                : new LogicalSessionStorage();

            storage.ConversationId = conversationId;
            storage.OpenTransactionJson = openTransactionJson;

            // Save the message to the session
            session.LogicalSessionStorage = storage;
        }

        JsonObject para = new JsonObject();
        para.Add("startPaused", false);
        para.Add("media", new[] { selectedMedia });

        Servermessage responseMessage = new Servermessage
        {
            version = "2",
            type = ServerMessageType.opened.ToString(),
            id = id,
            seq = serverSeq,
            clientseq = seq,
            parameters = para
        };

        var jsonResponse = JsonSerializer.Serialize(responseMessage);

        await SendMessageAsync(sessionId, jsonResponse);
    }

    private async Task HandleConnectionProbeAsync(string sessionId, string? id, int seq)
    {
        _logger.Log($"[{sessionId}] Connection probe. Conversation should not be logged and transcribed.", LogLevel.Info);
        await Task.CompletedTask;
    }

    private async Task HandleClientUpdateMessageAsync(string sessionId, string updateMessageJson)
    {
        _logger.Log($"[{sessionId}] Received an update message: {updateMessageJson}", LogLevel.Info);
        await Task.CompletedTask;
    }

    private async Task HandleClientCloseMessageAsync(string sessionId, string closeMessageJson, string? id, int clientseq, int serverSeq)
    {
        // Deserialize the close message
        Dictionary<string, object>? closeMessage = JsonSerializer.Deserialize<Dictionary<string, object>>(closeMessageJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (closeMessage == null || !closeMessage.TryGetValue("parameters", out var parametersObj) || parametersObj is not JsonElement parameters)
        {
            _logger.Log($"[{sessionId}] Invalid or missing 'parameters' in close message.", LogLevel.Warning);
            return;
        }

        // Extract the reason from the parameters
        if (!parameters.TryGetProperty("reason", out var reasonElement))
        {
            _logger.Log($"[{sessionId}] Missing 'reason' in parameters of close message.", LogLevel.Warning);
            return;
        }

        var reason = reasonElement.GetString();

        // Log the reason
        _logger.Log($"[{sessionId}] Received a close message. Reason: {reason}", LogLevel.Info);

        JsonObject param = new JsonObject();

        Servermessage responseMessage = new Servermessage
        {
            version = "2",
            type = ServerMessageType.closed.ToString(),
            id = id,
            seq = serverSeq,
            clientseq = clientseq,
            parameters = param
        };

        var jsonResponse = JsonSerializer.Serialize(responseMessage);

        // Send the message back to the client and the client will close the socket
        await SendMessageAsync(sessionId, jsonResponse);
    }

    private async Task HandleClientPingMessageAsync(string sessionId, string clientMessage, string? id, int clientseq, int serverSeq)
    {
        JsonObject param = new JsonObject();

        Servermessage responseMessage = new Servermessage
        {
            version = "2",
            type = ServerMessageType.pong.ToString(),
            id = id,
            seq = serverSeq,
            clientseq = clientseq,
            parameters = param
        };

        var jsonResponse = JsonSerializer.Serialize(responseMessage);
        await SendMessageAsync(sessionId, jsonResponse);
    }

    private async Task HandleClientErrorMessageAsync(string sessionId, string errorMessageJson)
    {
        ErrorMessage? errorMessage = JsonSerializer.Deserialize<ErrorMessage>(errorMessageJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (errorMessage == null || errorMessage.parameters == null)
        {
            _logger.Log($"[{sessionId}] Invalid or missing 'parameters' in error message.", LogLevel.Warning);
            return;
        }

        // Log the error details
        _logger.Log($"[{sessionId}] Error received: Code {errorMessage.parameters.code}, Message: {errorMessage.parameters.message}", LogLevel.Error);

        await Task.CompletedTask;
    }
}