# TAPI Live Engine Monitor App

A lightweight, high-performance Windows Telephony API (TAPI 3) service with an embedded asynchronous TCP server. The application runs natively from the Windows System Tray, captures hardware telephony events (Offering, Connected, Disconnected) in real-time, formats payloads into compliant ISO 8601 JSON streams, and broadcasts them to connected client socket loops.

---

## Table of Contents
1. [System Overview](#system-overview)
2. [Architecture Diagram](#architecture-diagram)
3. [Installation Requirements](#installation-requirements)
4. [Configuration Guide](#configuration-guide)
5. [API & Event Reference](#api--event-reference)
6. [Client Connection Examples](#client-connection-examples)
7. [Troubleshooting Guide](#troubleshooting-guide)

---

## System Overview
The **TAPI Live Engine Monitor** bridges legacy telecom/PBX infrastructure with modern software environments. It replaces volatile COM-based inheritance event-sinks with a robust, unmanaged wrapper allocation interface (`ITMediaSupport`), insulating production servers from memory leaks, cross-thread UI synchronization issues, and hardware-level driver drops.

### Key Features
* **Zero-Footprint UI:** Runs entirely within the Windows System Tray context.
* **Resilient COM Binding:** Uses dynamic bitmask extraction (`8` for `AUDIO`) bypassing unstable type library definitions.
* **Fault-Tolerant String Extraction:** Safe `TryGetCallInfo` interceptors protect the background thread loops from driver-level `E_FAIL` errors.
* **ISO 8601 Compliance:** Timestamps are strictly locked to `yyyy-MM-ddTHH:mm:ss`.
* **Integrated Bootstrapper:** Simple UI toggle to wire native execution into the `CurrentVersion\Run` Windows Registry hive.

---

## Architecture Diagram

```{r chunk-label, include=FALSE}
+------------------------------------------------------------+
|                  Windows Telephony (TAPI3)                 |
|       (PBX Hardware / Virtual Driver / Line Addresses)     |
+----------------------------------+-------------------------+
|
COM Interface | (ITTAPIEventNotification)
v
+------------------------------------------------------------+
|                    TapiMonitorApp Engine                   |
|  - Pattern Matching Interface Casting (ITMediaSupport)      |
|  - Safe E_FAIL Interceptors (TryGetCallInfo)               |
+----------------------------------+-------------------------+
|
JSON Event Broadcast | (ISO 8601 Formatting)
v
+------------------------------------------------------------+
|                 Asynchronous Local TCP Server              |
|                     (Default Port: 8080)                   |
+--------+-------------------------+----------------+--------+
|                         |                |
v                         v                v
+------------------+     +------------------+     +------------------+
| Progress ABL     |     | Node.js / Python |     | WebSockets /     |
| Socket Client    |     | microservice     |     | Third-Party CRM  |
+------------------+     +------------------+     +------------------+
```

---

## Installation Requirements

### System Pre-requisites
* **OS:** Windows 10, Windows 11, or Windows Server 2016/2019/2022.
* **Framework:** .NET 8.0 or .NET 9.0 Windows Forms Runtime.
* **Hardware Drivers:** Third-party Vendor TAPI Service Provider (TSP) drivers installed and properly configured in Windows **Phone and Modem** Options.

### Build Compilation Targets
> [!IMPORTANT]
> Because most legacy PBX system hardware drivers run strictly on 32-bit architecture, you **must** configure your Solution Platform targeting target configuration inside Visual Studio to **`x86`**. Compiling as `Any CPU` or `x64` will cause the application to fail to instantiate the underlying `TAPIClass` COM object (`REGDB_E_CLASSNOTREG`).

1. Open the project in Visual Studio.
2. Change the Build Configuration dropdown from `Any CPU` to **`x86`**.
3. Rebuild the solution.

---

## Configuration Guide

### Registry Keys (Run at Windows Startup)
When enabled via the UI context menu, the application registers its fully qualified path within the current user workspace hive:
* **Hive Path:** `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
* **Key Name:** `TapiMonitorApp`
* **Type:** `REG_SZ`

### Working Directory Override Note
To prevent path resolution crashes when Windows initializes the program at system boot (which defaults execution contexts to `C:\Windows\System32`), the bootstrapper forces the process runtime boundary directory back to the executable layout source via:
```csharp
Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

API & Event Reference
All network stream communication emits structural JSON string buffers terminating with a clean newline character (\n) for stream chunk boundaries.

1. Incoming Call (OnIncomingCall)
Emitted immediately when a telephone line captures an offering loop packet flag.

JSON
{
  "Type": "OnIncomingCall",
  "Timestamp": "2026-05-16T11:17:35.243000Z",
  "Data": {
    "CallID": 4729104,
    "CallerNumber": "5551234567",
    "CallerName": "John Doe",
    "CalledNumber": "5559876543",
    "CalledName": "Main Support Line",
    "StartTime": "2026-05-16T11:17:35"
  }
}
2. Call Connected (OnCallConnected)
Emitted the instant the agent answers the call or the line links into active channels.

JSON
{
  "Type": "OnCallConnected",
  "Timestamp": "2026-05-16T11:17:42.112000Z",
  "Data": {
    "CallID": 4729104,
    "CallerNumber": "5551234567",
    "ConnectedTime": "2026-05-16T11:17:42",
    "DurationBeforeAnswer": 6.87
  }
}
3. Call Ended (OnCallEnded)
Emitted upon call termination/hang-up.

JSON
{
  "Type": "OnCallEnded",
  "Timestamp": "2026-05-16T11:19:15.554000Z",
  "Data": {
    "CallID": 4729104,
    "CallerNumber": "5551234567",
    "StartTime": "2026-05-16T11:17:35",
    "EndTime": "2026-05-16T11:19:15",
    "TotalDuration": 100.31
  }
}
Client Connection Examples
