using GenesysAudioHookServer.Helpers;
using GenesysAudioHookServer.Processors;

namespace GenesysAudioHookServer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("Starting Genesys Audio Hook Server...");
        Console.WriteLine("Press Control + C to stop the server.");

        var logFilePath = Environment.GetEnvironmentVariable("LOG_FILE_PATH") ?? "c:/temp/log.txt";
        var serverUrl = Environment.GetEnvironmentVariable("SERVER_URL") ?? "http://localhost:5000/ws/";

        // Logs to both console and file
        var logger = new Logger(logFilePath);

        // Cancellation token for graceful shutdown
        var cts = new CancellationTokenSource();

        // Initialize the ProcessHook (Inheriting from WebSocketManager)
        var processHook = new ProcessHook(logger, cts.Token);

        // Handle Control + C to shut down
        Console.CancelKeyPress += async (sender, eventArgs) =>
        {
            logger.Log("Control + C detected. Initiating shutdown...", LogLevel.Info);

            eventArgs.Cancel = true; // Prevent abrupt termination
            cts.Cancel();

            // Wait for ongoing tasks to complete
            await Task.WhenAny(Task.Delay(10000), Task.Run(() => cts.Token.WaitHandle.WaitOne()));

            logger.Log("Shutdown complete.", LogLevel.Info);
            Environment.Exit(0); // Forcefully terminate the process
        };

        try
        {
            await processHook.StartHttpListenerAsync(serverUrl);
        }
        catch (OperationCanceledException)
        {
            logger.Log("Server shutdown requested.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            logger.Log($"Unexpected error: {ex.Message}", LogLevel.Error);
        }
        finally
        {
            logger.Log("Server has stopped.", LogLevel.Info);
        }
    }
}
