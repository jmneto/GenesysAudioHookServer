using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using GenesysAudioHookServer.Helpers;

namespace GenesysAudioHookServer.Server;

internal abstract class WebSocketManager
{
    // Public Methods
    ///////////////////////////////////////////////////////////


    /// <summary>
    /// Starts the HTTP listener for WebSocket connections.
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public async Task StartHttpListenerAsync(string url)
    {
        var httpListener = new HttpListener();
        httpListener.Prefixes.Add(url);
        httpListener.Start();
        _logger.Log($"WebSocket server started at {url}", LogLevel.Info);

        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var webSocketContext = await context.AcceptWebSocketAsync(null);

                    // assing header value from Audiohook-Session-Id to sessionId
                    var headers = context.Request.Headers;
                    string? sessionId = headers?["Audiohook-Session-Id"]?.ToString();

                    if (string.IsNullOrEmpty(sessionId))
                    {
                        _logger.Log("Session ID is null or empty.", LogLevel.Error);
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    _logger.Log($"New WebSocket session: {sessionId}", LogLevel.Info);

                    await HandleWebSocketAsync(webSocketContext.WebSocket, sessionId);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        catch (HttpListenerException ex)
        {
            _logger.Log($"HTTP Listener error: {ex.Message}", LogLevel.Error);
        }
        catch (WebSocketException ex)
        {
            _logger.Log($"WebSocket error: {ex.Message}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            _logger.Log($"Unexpected error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            httpListener.Stop();
            _logger.Log("HTTP listener stopped.", LogLevel.Info);
        }
    }


    // Protected Methods to be used in the child classes
    ///////////////////////////////////////////////////////////

    // Constructor
    protected readonly Logger _logger;
    protected CancellationToken _cancellationToken;

    protected WebSocketManager(Logger logger, CancellationToken cancellationToken)
    {
        _logger = logger;
        _cancellationToken = cancellationToken;
    }


    /// <summary>
    /// Sends a message to the client over the WebSocket connection.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    protected virtual async Task SendMessageAsync(string sessionId, string message)
    {
        if (!TryGetSession(sessionId, out var session))
            return;

        if (session!.WebSocket == null)
        {
            _logger.Log($"WebSocket for session {sessionId} is null.", LogLevel.Warning);
            return;
        }

        var webSocket = session.WebSocket;

        if (webSocket.State == WebSocketState.Open)
        {
            await _sendSemaphore.WaitAsync();
            try
            {
                session.ServerSeq++;
                var messageBytes = Encoding.UTF8.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, _cancellationToken);
                _logger.Log($"Sent message to client: {sessionId} {session.ServerSeq} {message} ", LogLevel.Info);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Sends binary data to the client over the WebSocket connection.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    protected async Task SendBinaryAsync(string sessionId, byte[] data)
    {
        if (!TryGetSession(sessionId, out var session))
            return;

        if (session!.WebSocket == null)
        {
            _logger.Log($"WebSocket for session {sessionId} is null.", LogLevel.Warning);
            return;
        }

        var webSocket = session.WebSocket;

        if (webSocket.State == WebSocketState.Open)
        {
            await _sendSemaphore.WaitAsync();
            try
            {
                session.ServerSeq++;
                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, _cancellationToken);
                _logger.Log($"Sent binary data to client: {data.Length} bytes", LogLevel.Info);
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }
    }

    protected virtual async Task HandleIncomingMessageAsync(string sessionID, string message)
    {
        // Default implementation
        await Task.CompletedTask;
    }

    protected virtual async Task HandleIncomingBytesAsync(string sessionId, byte[] data)
    {
        // Default implementation
        await Task.CompletedTask;
    }


    // Stores the WebSocket session information.
    protected class WebSocketSession
    {
        public string? SessionId { get; set; }
        public WebSocket? WebSocket { get; set; }
        public int ServerSeq { get; set; } = 1;  // Starts at 1 because messages are creaated elsewhere and this is the current global ServerReq. The Client (genesys) already has the 0 so If we send another 0 it fails
        public int ClientSeq { get; set; } = 0;
        public object? LogicalSessionStorage { get; set; } = new object();
    }

    // Private Members
    //////////////////////


    private readonly ConcurrentDictionary<string, WebSocketSession> _activeSessions = new();

    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private async Task HandleWebSocketAsync(WebSocket webSocket, string sessionId)
    {
        AddSession(sessionId, webSocket);

        var buffer = new byte[1024 * 4];
        try
        {
            while (webSocket.State == WebSocketState.Open && !_cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.Log($"Session {sessionId} closed by client.", LogLevel.Info);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", _cancellationToken);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.Log($"Received text msg Session: {sessionId}: {message}", LogLevel.Info);
                    await HandleIncomingMessageAsync(sessionId, message);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var data = buffer[..result.Count];
                    _logger.Log($"Received binary data Session: {sessionId}: {data.Length} bytes", LogLevel.Info);
                    await HandleIncomingBytesAsync(sessionId, data);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error in WebSocket session {sessionId}: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            await RemoveSession(sessionId);
        }
    }


    private async Task CloseWebSocketAsync(string sessionId, WebSocketCloseStatus code, string humanReadableMessage)
    {
        if (!TryGetSession(sessionId, out var session))
            return;

        if (session!.WebSocket == null || session.WebSocket.State != WebSocketState.Open)
        {
            _logger.Log($"WebSocket for session {sessionId} is not open.", LogLevel.Warning);
            return;
        }

        try
        {
            // Close the WebSocket connection
            await session.WebSocket.CloseAsync(code, humanReadableMessage, _cancellationToken);
            _logger.Log($"WebSocket for session {sessionId} closed with code {code} and message: {humanReadableMessage}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _logger.Log($"Error while closing WebSocket for session {sessionId}: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            // Remove the session from the active sessions
            await RemoveSession(sessionId);
        }
    }

    protected bool TryGetSession(string sessionId, out WebSocketSession? wss)
    {
        wss = null;
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            wss = session;
            return true;
        }
        _logger.Log($"Session {sessionId} not found or WebSocket is null.", LogLevel.Warning);
        return false;
    }


    private void AddSession(string sessionId, WebSocket webSocket)
    {
        var session = new WebSocketSession
        {
            SessionId = sessionId,
            WebSocket = webSocket,
        };
        if (_activeSessions.TryAdd(sessionId, session))
        {
            _logger.Log($"Session {sessionId} added.", LogLevel.Info);
        }
    }

    private async Task RemoveSession(string sessionId)
    {

        if (TryGetSession(sessionId, out var session))
        {
            var webSocket = session!.WebSocket;

            if (webSocket != null)
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", _cancellationToken);
                    webSocket.Dispose();
                }

            if (_activeSessions.TryRemove(sessionId, out _))
            {
                _logger.Log($"Session {sessionId} removed.", LogLevel.Info);
            }
        }
    }
}

