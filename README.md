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
