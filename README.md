# Genesys .NET C# AudioHook Server

This repository provides a reference implementation in **.NET C#** for the [Genesys AudioHook protocol](https://developer.genesys.cloud/devapps/audiohook/). 

The AudioHook server is designed to handle WebSocket connections and process audio streams according to the Genesys AudioHook Protocol specifications, including both control messages and audio data

The [AudioHook Monitor](https://help.mypurecloud.com/articles/audiohook-monitor-overview/) Genesys (the client) streams audio to the server (this code).


> **Note:** This code serves as a sample blueprint to help you get started building an AudioHook server and testing protocol compliance.


## Overview

The Genesys AudioHook Monitor (client) streams audio to this server implementation. This server receives, logs, and processes the audio stream in real time.


## Getting Started

### Prerequisites

1. [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet) navigate to the project directory.  
2. (Optional) Set the following environment variables to override defaults:   
   - `LOG_FILE_PATH`: Path to the log file (default: `C:/temp/log.txt`)    
   - `SERVER_URL`: WebSocket server URL (default: `http://localhost:5000/ws/`)    
3. Build and run the server:  

   ```
   dotnet run --project GenesysAudioHookServer
   ```

4. The server will start and listen for WebSocket connections. Press `CTRL+C` to stop the server gracefully.


## Testing the Server

To test the server, use the [Genesys AudioHook Sample Service Test Client](https://github.com/purecloudlabs/audiohook-reference-implementation/blob/main/README.md#test-client).

In the `client` directory, there is a simple command-line client. It establishes a connection and synthesizes an audio stream of a 1 kHz tone (stereo or mono depending on which media format the server accepted). 

#### Running the Client

Replace the URI, API key, and client secret with your own. Below is an example using the sample API key and client secret from the protocol documentation:

```

cd client
ts-node src/index.ts --uri ws://localhost:5000/ws --api-key SGVsbG8sIEkgYW0gdGhlIEFQSSBrZXkh --client-secret TXlTdXBlclNlY3JldEtleVRlbGxOby0xITJAMyM0JDU=
```

Pressing `CTRL+C` initiates a close transaction followed by disconnect and exit.

#### Using WAV Files as Audio Source

The client supports WAV files as an audio source. Mono or stereo WAV files are supported. They must be encoded in:
- 16-bit linear PCM (format tag 1), or
- PCMU (format tag 7) at an 8000Hz sample rate.

Example:

```
cd client
ts-node src/index.ts --uri ws://localhost:5000/ws --api-key SGVsbG8sIEkgYW0gdGhlIEFQSSBrZXkh --client-secret TXlTdXBlclNlY3JldEtleVRlbGxOby0xITJAMyM0JDU= --wavfile example.wav
```

Once the WAV file ends, the session closes, and the client exits. A close can also be initiated with `CTRL+C` at any time.

---

For more details on the Genesys AudioHook protocol, refer to the [Gensys AudioHook official documentation](https://developer.genesys.cloud/devapps/audiohook/).


# Additional Resources
- [Genesys AudioHook Protocol](https://developer.genesys.cloud/devapps/audiohook/)
- [Genesys AudioHook Monitor](https://help.mypurecloud.com/articles/audiohook-monitor-overview/)
- [Genesys AudioHook Sample Service](https://github.com/purecloudlabs/audiohook-reference-implementation/tree/main)

   
