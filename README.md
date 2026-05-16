# TAPI Monitor — Technical Documentation

**Version:** 1.0  
**Framework:** .NET 8 / Windows Forms  
**Protocol:** TCP · localhost:1471 · UTF-8 JSON Lines  

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Architecture Diagram](#2-architecture-diagram)
3. [Installation Requirements](#3-installation-requirements)
4. [Configuration Guide](#4-configuration-guide)
5. [API & Event Reference](#5-api--event-reference)
6. [Client Connection Examples](#6-client-connection-examples)
7. [Troubleshooting Guide](#7-troubleshooting-guide)

---

## 1. System Overview

TAPI Monitor is a Windows system-tray application that acts as a **bridge between the Windows telephony subsystem (TAPI 3.0) and any TCP client** on the local machine.

The application:

- Runs silently in the background as a Windows system-tray icon.
- Registers with Microsoft TAPI 3.0 to receive telephony events from any connected device — analogue modems, ISDN adapters, PBX CTI links, or VoIP/SIP TAPI Service Providers (TSPs).
- Translates raw TAPI COM events into structured JSON payloads.
- Broadcasts those payloads over a TCP socket server bound exclusively to `127.0.0.1:1471`.
- Supports any number of simultaneous TCP clients (each receives every event).
- Sends a periodic `PING` heartbeat every 30 seconds so clients can detect liveness without polling.
- Provides a colour-coded live log window accessible from the tray icon.
- Displays system notification balloons for incoming calls.

### Design Philosophy

| Decision | Rationale |
|---|---|
| **localhost-only binding** | Eliminates authentication complexity; no credentials, no TLS. Access is governed by OS-level network isolation. |
| **JSON Lines format** | One self-contained JSON object per line. Easy to parse in any language without a framing protocol. |
| **LF delimiter (`\n`)** | Compatible with `readline()` in Python, `StreamReader.ReadLineAsync()` in C#, and `split('\n')` in JavaScript. |
| **TAPI 3.0 COM interop** | Native Windows telephony API; works with any TSP regardless of vendor. |
| **No persistence** | Events are ephemeral broadcast; clients that were not connected at event time do not receive missed events. |

---

## 2. Architecture Diagram

### Component Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Windows OS                                    │
│                                                                      │
│  ┌──────────────┐   TAPI COM events   ┌────────────────────────┐   │
│  │  Telephony   │ ─────────────────► │   TapiEventMonitor     │   │
│  │  Hardware    │                     │   (COM/ITTAPIEvent-     │   │
│  │  (modem/PBX/ │                     │    Notification)        │   │
│  │   VoIP TSP)  │                     └──────────┬─────────────┘   │
│  └──────────────┘                                │                  │
│                                                  │ managed events   │
│                                    ┌─────────────▼─────────────┐   │
│                                    │   TapiMonitorService       │   │
│                                    │   (orchestration)          │   │
│                                    └──────┬────────────┬────────┘   │
│                                           │            │            │
│                                    log    │            │  JSON      │
│                             ┌─────────────▼──┐  ┌─────▼──────────┐│
│                             │   LogForm      │  │ TcpEventServer ││
│                             │   (RichTextBox)│  │ (127.0.0.1:    ││
│                             └────────────────┘  │  1471)         ││
│                                                  └──────┬─────────┘│
│                                                         │           │
│  ┌──────────────────────────────────────────────────────┘           │
│  │  TCP Clients (any number)                                        │
│  │                                                                  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  │ Python app   │  │ Node.js app  │  │  C# service  │  …       │
│  │  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────────┘
```

### Event Flow Sequence Diagram

```
  TAPI HW       TapiEventMonitor     TapiMonitorService    TcpEventServer    TCP Client
     │                  │                    │                   │                │
     │──CS_OFFERING────►│                    │                   │                │
     │                  │──IncomingCall──────►                   │                │
     │                  │                    │──BroadcastIncoming►                │
     │                  │                    │                   │──JSON\n────────►│
     │                  │                    │                   │                │
     │──CS_CONNECTED───►│                    │                   │                │
     │                  │──CallConnected─────►                   │                │
     │                  │                    │──BroadcastConnected►               │
     │                  │                    │                   │──JSON\n────────►│
     │                  │                    │                   │                │
     │──CS_DISCONNECTED►│                    │                   │                │
     │                  │──CallEnded─────────►                   │                │
     │                  │                    │──BroadcastEnded───►                │
     │                  │                    │                   │──JSON\n────────►│
     │                  │                    │                   │                │
     │                  │              (30s timer)               │                │
     │                  │                    │──BroadcastPing────►                │
     │                  │                    │                   │──PING\n────────►│
```

### Thread Model

```
Main (STA) Thread
  └── WinForms message loop
  └── TrayApplicationContext
  └── LogForm (UI updates via BeginInvoke)

TAPI COM Thread (managed by TAPI runtime)
  └── TapiEventMonitor.Event() callback
  └── → fires managed events (any thread)

ThreadPool Threads (Task.Run)
  └── TcpEventServer.AcceptLoop     (one task)
  └── TcpEventServer.PingLoop       (one task)
  └── TcpEventServer.MonitorClient  (one task per client)
```

---

## 3. Installation Requirements

### Runtime Requirements

| Component | Minimum Version | Notes |
|---|---|---|
| **Windows OS** | Windows 7 SP1 | Windows 10/11 recommended |
| **.NET Runtime** | .NET 8 Windows Desktop Runtime | Download from microsoft.com/net |
| **TAPI** | 3.0 (built into Windows) | No additional install required |
| **Telephony Device** | Any TAPI-compatible device | See supported devices below |
| **Privileges** | Administrator | Required for TAPI line-ownership registration |

### Supported Telephony Devices

- Analogue modems with TAPI drivers (most USB/PCI modems)
- ISDN adapters with TAPI Service Providers
- PBX systems with CTI/TAPI integration (e.g., Avaya, Cisco, Mitel)
- VoIP soft-phones with TAPI TSP (e.g., 3CX, Lync/Teams Phone System)
- Hardware IP phones with TAPI drivers

### Build Requirements (Developers)

| Tool | Version |
|---|---|
| Visual Studio 2022 | 17.8+ (Community edition is free) |
| .NET SDK | 8.0+ |
| Windows SDK | Included with Visual Studio |
| TAPI 3.0 Type Library | `C:\Windows\System32\tapi3.dll` (register as COM reference) |

### Registering the TAPI COM Reference

In Visual Studio:

1. Right-click the project → **Add** → **COM Reference…**
2. Find **TAPI 3.0** (GUID `{21D6D48E-A88B-11D0-83DD-00AA003CCABD}`)
3. Click OK — Visual Studio generates the interop assembly automatically.

For SDK-style (`dotnet build`) builds without Visual Studio, the `.csproj` already includes the `<COMReference>` element; ensure `tlbimp` is available on the PATH (included with Windows SDK).

### Firewall

No inbound firewall rule is required — the server binds exclusively to `127.0.0.1` (loopback). No traffic crosses any network interface.

---

## 4. Configuration Guide

All configuration is embedded in source constants and the `.csproj`; there is no external config file in v1.0.

### Compile-Time Settings

| Setting | Location | Default | Description |
|---|---|---|---|
| `Port` | `TcpEventServer.cs` | `1471` | TCP listening port |
| `PingIntervalSec` | `TcpEventServer.cs` | `30` | Seconds between PING heartbeats |
| `MaxLines` | `LogForm.cs` | `2000` | Log window line buffer before trimming |
| UAC level | `app.manifest` | `requireAdministrator` | Change to `asInvoker` for non-admin TAPI devices |

### Changing the Port

```csharp
// TcpEventServer.cs  — line 19
public const int Port = 1471;   // ← Change this value
```

Rebuild and re-deploy. If using the Python/JS/C# client examples, update their `PORT` constant to match.

### Running Without Administrator Privileges

Some TAPI devices (particularly software-based TSPs) work without elevation. To remove the UAC prompt:

1. Open `app.manifest`.
2. Change `level="requireAdministrator"` to `level="asInvoker"`.
3. Rebuild.

Test by launching without elevation — if TAPI initialisation fails, the app will log the error and continue running (TCP server remains active).

### Auto-Start on Login

To start TAPI Monitor automatically with Windows:

1. Press **Win + R** → type `shell:startup` → press Enter.
2. Create a shortcut to `TapiMonitor.exe` in the Startup folder.

For a more robust approach, install as a Windows Service wrapper using NSSM or a similar tool. Note: system tray icons require an interactive desktop session.

### Logging to File (Optional)

The base application logs only to the in-memory `LogForm`. To add file logging, uncomment the Serilog package references in `.csproj` and add a `Serilog.Sinks.File` sink in `TapiMonitorService`.

---

## 5. API & Event Reference

### Message Format

Every event is a single-line UTF-8 JSON object terminated by a Line Feed (`\n`, `0x0A`). There is no Carriage Return before the Line Feed.

```
{"Type":"...","Timestamp":"...","Data":{...}}\n
```

| Field | Type | Description |
|---|---|---|
| `Type` | string | Event type identifier (see below) |
| `Timestamp` | ISO 8601 | Server wall-clock time of the event |
| `Data` | object | Event-specific payload |

### Event Types

---

#### `OnIncomingCall`

Fired immediately when TAPI signals a new inbound call (`CS_OFFERING` state).

**Data fields:**

| Field | Type | Description |
|---|---|---|
| `CallID` | integer | Unique call identifier for this session |
| `CallerNumber` | string | Caller's phone number (E.164 or raw) |
| `CallerName` | string | Caller name from Caller-ID (may be empty) |
| `CalledNumber` | string | Dialled number / DNIS |
| `CalledName` | string | Name of the called line / hunt group |
| `StartTime` | ISO 8601 | Time the call was first detected |

**Example:**

```json
{
  "Type": "OnIncomingCall",
  "Timestamp": "2024-01-15T14:30:25.1234567",
  "Data": {
    "CallID": 12345,
    "CallerNumber": "+1-555-1234",
    "CallerName": "John Smith",
    "CalledNumber": "+1-555-0000",
    "CalledName": "Main Line",
    "StartTime": "2024-01-15T14:30:25"
  }
}
```

---

#### `OnCallConnected`

Fired when the call transitions to `CS_CONNECTED` (call answered).

**Data fields:**

| Field | Type | Description |
|---|---|---|
| `CallID` | integer | Matches the `CallID` from `OnIncomingCall` |
| `CallerNumber` | string | Caller's phone number |
| `ConnectedTime` | ISO 8601 | Time the call was answered |
| `DurationBeforeAnswer` | float | Seconds from ring to answer |

**Example:**

```json
{
  "Type": "OnCallConnected",
  "Timestamp": "2024-01-15T14:30:33.4567890",
  "Data": {
    "CallID": 12345,
    "CallerNumber": "+1-555-1234",
    "ConnectedTime": "2024-01-15T14:30:33",
    "DurationBeforeAnswer": 8.3
  }
}
```

---

#### `OnCallEnded`

Fired when the call transitions to `CS_DISCONNECTED` or `CS_IDLE`.

**Data fields:**

| Field | Type | Description |
|---|---|---|
| `CallID` | integer | Matches earlier events for this call |
| `CallerNumber` | string | Caller's phone number |
| `StartTime` | ISO 8601 | Time the call first arrived |
| `EndTime` | ISO 8601 | Time the call disconnected |
| `TotalDuration` | float | Seconds from first ring to disconnect |

**Example:**

```json
{
  "Type": "OnCallEnded",
  "Timestamp": "2024-01-15T14:35:50.9876543",
  "Data": {
    "CallID": 12345,
    "CallerNumber": "+1-555-1234",
    "StartTime": "2024-01-15T14:30:25",
    "EndTime": "2024-01-15T14:35:50",
    "TotalDuration": 325.0
  }
}
```

---

#### `OnTapiError`

Fired when the TAPI subsystem reports an error. Clients should log these and optionally alert operators.

**Data fields:**

| Field | Type | Description |
|---|---|---|
| `Message` | string | Short human-readable error description |
| `ErrorDetails` | string | Full exception message / TAPI HRESULT |

**Example:**

```json
{
  "Type": "OnTapiError",
  "Timestamp": "2024-01-15T14:30:00.0000000",
  "Data": {
    "Message": "TAPI Initialisation Failed",
    "ErrorDetails": "System.Runtime.InteropServices.COMException (0x80040154): ..."
  }
}
```

---

#### `PING`

Sent every 30 seconds as a liveness heartbeat. If a client does not receive a PING within 60 seconds, it should assume the server has crashed and attempt reconnection.

**Data fields:**

| Field | Type | Description |
|---|---|---|
| `PingNumber` | integer | Monotonically increasing counter (resets on restart) |
| `ServerTime` | ISO 8601 | Current server time |
| `ActiveClients` | integer | Number of connected TCP clients at time of ping |

**Example:**

```json
{
  "Type": "PING",
  "Timestamp": "2024-01-15T14:30:55.1234567",
  "Data": {
    "PingNumber": 1,
    "ServerTime": "2024-01-15T14:30:55.1234567",
    "ActiveClients": 2
  }
}
```

---

### Call Lifecycle State Machine

```
  ┌─────────────────────────────────────────────────────────┐
  │                                                         │
  │   [Phone rings]                                         │
  │        │                                                │
  │        ▼                                                │
  │   OnIncomingCall ──────────────────────────────────┐   │
  │        │                                            │   │
  │        │  (call answered)          (missed / hung   │   │
  │        ▼                            up before       │   │
  │   OnCallConnected                   answer)         │   │
  │        │                                │           │   │
  │        │  (call ends)                   │           │   │
  │        ▼                                ▼           │   │
  │   OnCallEnded ◄────────────────────────┘           │   │
  │        │                                            │   │
  │        │         (error at any point)               │   │
  │        ▼                                            │   │
  │   [Idle]         OnTapiError ◄─────────────────────┘   │
  │                                                         │
  └─────────────────────────────────────────────────────────┘
```

---

## 6. Client Connection Examples

All examples connect to `127.0.0.1:1471`, read newline-delimited JSON, and parse each event type.

---

### Python (3.8+)

```python
import json, socket, time

def connect(host="127.0.0.1", port=1471):
    while True:
        try:
            with socket.create_connection((host, port)) as sock:
                buf = b""
                while True:
                    chunk = sock.recv(4096)
                    if not chunk:
                        break
                    buf += chunk
                    while b"\n" in buf:
                        line, buf = buf.split(b"\n", 1)
                        event = json.loads(line.decode("utf-8"))
                        handle(event)
        except Exception as e:
            print(f"Error: {e}")
        time.sleep(5)   # reconnect after 5 s

def handle(event):
    t    = event["Type"]
    data = event["Data"]
    if t == "OnIncomingCall":
        print(f"📞 Call from {data['CallerNumber']}")
    elif t == "OnCallEnded":
        print(f"✖ Call ended after {data['TotalDuration']:.0f}s")
    elif t == "PING":
        pass   # ignore heartbeat

connect()
```

---

### JavaScript / Node.js

```javascript
const net = require("net");

function connect() {
  const client = new net.Socket();
  let buffer = "";

  client.connect(1471, "127.0.0.1", () => console.log("Connected"));

  client.on("data", chunk => {
    buffer += chunk.toString("utf8");
    const lines = buffer.split("\n");
    buffer = lines.pop();                 // keep partial last line

    for (const line of lines) {
      if (!line.trim()) continue;
      const event = JSON.parse(line);
      handle(event);
    }
  });

  client.on("close", () => setTimeout(connect, 5000));
  client.on("error", err => console.error(err.message));
}

function handle({ Type, Data }) {
  if (Type === "OnIncomingCall")
    console.log(`📞 Call from ${Data.CallerNumber}`);
  else if (Type === "OnCallEnded")
    console.log(`✖ Ended after ${Data.TotalDuration.toFixed(0)}s`);
}

connect();
```

---

### C# (.NET 6+)

```csharp
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

async Task ListenAsync(CancellationToken ct = default)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            using var tcp    = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", 1471, ct);
            using var reader = new StreamReader(tcp.GetStream(), Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                string type = root.GetProperty("Type").GetString()!;
                var data = root.GetProperty("Data");

                switch (type)
                {
                    case "OnIncomingCall":
                        Console.WriteLine($"📞 {data.GetProperty("CallerNumber")}");
                        break;
                    case "OnCallEnded":
                        Console.WriteLine($"✖ {data.GetProperty("TotalDuration")}s");
                        break;
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Console.WriteLine($"Error: {ex.Message}");
            await Task.Delay(5000, ct);
        }
    }
}

await ListenAsync();
```

---

### PowerShell

```powershell
$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("127.0.0.1", 1471)
$reader = New-Object System.IO.StreamReader($client.GetStream())

while ($true) {
    $line = $reader.ReadLine()
    if (-not $line) { break }
    $event = $line | ConvertFrom-Json
    Write-Host "$($event.Type): $($event.Data | ConvertTo-Json -Compress)"
}
```

---

### Command Line (Windows — telnet)

> **Enable Telnet:** `dism /online /Enable-Feature /FeatureName:TelnetClient`

```
telnet 127.0.0.1 1471
```

Events will stream to the terminal in real time. Press **Ctrl + ]** then type `quit` to disconnect.

---

### Command Line (Linux/WSL — netcat)

```bash
nc 127.0.0.1 1471
# or
nc -q 0 127.0.0.1 1471
```

Pretty-print with `jq`:

```bash
nc 127.0.0.1 1471 | while IFS= read -r line; do echo "$line" | jq .; done
```

---

### Liveness / PING Watchdog Pattern

Clients should implement a watchdog timer that reconnects if no event (including PING) has been received within **60 seconds**.

```python
import time, threading

last_event = time.monotonic()

def watchdog():
    while True:
        time.sleep(10)
        if time.monotonic() - last_event > 60:
            print("No event in 60s — reconnecting …")
            # trigger reconnect logic here

threading.Thread(target=watchdog, daemon=True).start()
```

---

## 7. Troubleshooting Guide

### Symptom: Application fails to start / crashes immediately

| Check | Resolution |
|---|---|
| .NET 8 Windows Desktop Runtime not installed | Download from `https://dot.net` |
| Running without administrator privileges | Right-click → Run as administrator, or change UAC level in `app.manifest` |
| Another instance already running | Check the system tray; only one instance is allowed |

---

### Symptom: "TAPI Initialisation Failed" error in log

**Cause:** TAPI 3.0 could not start, or no addresses were registered.

**Steps:**

1. Open **Phone and Modem** in Control Panel (`telephon.cpl`).
2. Verify at least one line is listed under the **Modems** or **Advanced** tab.
3. Check Windows Event Viewer → **Application** log for `TAPI` source errors.
4. Ensure the **Telephony** Windows service is running:
   ```
   services.msc  →  Telephony  →  Start
   ```
5. If using a VoIP TSP, reinstall the TSP driver and reboot.

---

### Symptom: TCP clients cannot connect

| Check | Resolution |
|---|---|
| TAPI Monitor not running | Look for the icon in the system tray notification area |
| Wrong port | Default is `1471`; verify with `netstat -an | findstr 1471` |
| Connecting from a remote machine | Only `127.0.0.1` is supported; use an SSH tunnel for remote access |
| Port already in use | Change `Port` constant and rebuild |

**Verify the server is listening:**

```cmd
netstat -an | findstr 1471
```

Expected output:
```
  TCP    127.0.0.1:1471    0.0.0.0:0    LISTENING
```

---

### Symptom: Events are received but CallerNumber / CallerName is empty

**Cause:** Caller-ID information has not been delivered by the TAPI driver yet, or the telephony line does not support Caller-ID.

**Mitigations:**

- Subscribe to `OnCallInfoChange` events in your client and update caller data when `CallerNumber` becomes available (the server already handles late Caller-ID updates internally).
- Confirm Caller-ID is enabled on your telephone line with your carrier.
- Check if your TAPI driver has a "Caller-ID" or "CNID" setting in its configuration panel.

---

### Symptom: Application shows in tray but no call events arrive

1. Place a test call to the monitored line.
2. Open the Event Log window (double-click tray icon or right-click → Show Event Log).
3. Look for green "Monitoring: …" lines during startup — if none appear, no TAPI addresses were registered.
4. Check that the telephony device is connected and its driver is installed:
   ```cmd
   devmgmt.msc  →  Modems  (or Network adapters for VoIP)
   ```
5. Some PBX TAPI drivers require the phone to be in a **logged-in** state before events flow. Log in to your desk phone / softphone first.

---

### Symptom: JSON parse errors in client

**Cause:** Client is not correctly reading newline-delimited messages.

**Common mistakes:**

```python
# WRONG — reads fixed bytes, may split mid-JSON
data = sock.recv(1024)
event = json.loads(data)

# CORRECT — use readline() or accumulate until \n
buf = b""
while b"\n" not in buf:
    buf += sock.recv(4096)
line, buf = buf.split(b"\n", 1)
event = json.loads(line)
```

---

### Symptom: Memory grows over time (many clients)

Each connected TCP client holds an open `TcpClient` object and a monitoring `Task`. Disconnected-but-not-closed clients are detected on the next broadcast attempt and removed. If clients disconnect abnormally, they will be cleaned up within 30 seconds (next PING broadcast).

Verify client count via:
- The **Connected clients:** menu item in the tray icon context menu.
- The `ActiveClients` field in `PING` events.

---

### Symptom: High CPU usage

The application uses fully async I/O (`await`-based) and should have near-zero CPU usage between events. If CPU is unexpectedly high:

1. Check that the TAPI driver is not flooding events (some drivers emit `TE_CALLINFOCHANGE` continuously).
2. Reduce the number of monitored addresses by filtering in `TapiEventMonitor.RegisterAllAddresses()`.
3. Profile with **Visual Studio Diagnostic Tools** or **dotnet-trace**.

---

### Log Colour Reference

| Colour | Level | Meaning |
|---|---|---|
| 🟢 Green | `Success` | Operation completed successfully |
| 🔴 Red | `Error` | Exception or failure — requires attention |
| 🟡 Yellow | `Warning` | Degraded but non-fatal condition |
| 🔵 Cyan | `Event` | Telephony event (incoming, connected, ended) |
| 🟣 Magenta | `Tapi` | TAPI subsystem diagnostic message |
| ⬜ Gray | `Info` | General informational trace |

---

### Collecting a Diagnostic Log

1. Right-click the tray icon → **Show Event Log**.
2. Reproduce the issue.
3. Select All (`Ctrl+A`) in the log window, Copy (`Ctrl+C`), paste into a text file.
4. Include the log, Windows version, TAPI driver name/version, and telephony device model when reporting issues.

---

*TAPI Monitor — © 2024 — MIT Licence*
