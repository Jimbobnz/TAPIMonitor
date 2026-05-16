using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TAPI3Lib;
using TapiMonitorApp.Models;
using TapiMonitorApp.Networking;

namespace TapiMonitorApp.Telephony
{
    public class TapiEngine
    {
        private TAPIClass? _tapiManager;
        private int _cookie;
        private readonly LocalTcpServer _server;

        private readonly Dictionary<int, DateTime> _callStartTimes = new();
        private readonly Dictionary<int, DateTime> _callConnectedTimes = new();

        public event Action<string, LocalTcpServer.LogType>? OnLog;
        public event Action<string, string>? OnIncomingCallAlert;

        public TapiEngine(LocalTcpServer server)
        {
            _server = server;
        }

        public bool Initialize()
        {
            try
            {
                // Instantiate the underlying COM component object
                _tapiManager = new TAPIClass();
                _tapiManager.Initialize();

                // MATCHED TO YOUR ENVIRONMENT: Using the double _Event_Event signature
                _tapiManager.ITTAPIEventNotification_Event_Event += OnTapiEvent;
                _tapiManager.EventFilter = (int)(TAPI_EVENT.TE_CALLNOTIFICATION | TAPI_EVENT.TE_CALLSTATE);

                IEnumAddress addressEnum = _tapiManager.EnumerateAddresses();
                ITAddress address;
                uint fetched = 0;
                int addressCount = 0;

                addressEnum.Next(1, out address, ref fetched);
                while (fetched > 0 && address != null)
                {
                    // Cast the address to ITMediaSupport to safely check its media types
                    if (address is ITMediaSupport mediaSupport)
                    {
                        // Fix: Use raw integer 8 (the underlying TAPI constant for TAPIMEDIATYPE.AUDIO)
                        if ((mediaSupport.MediaTypes & 8) != 0)
                        {
                            _cookie = _tapiManager.RegisterCallNotifications(address, true, true, 8, 0);
                            addressCount++;
                        }
                    }

                    addressEnum.Next(1, out address, ref fetched);
                }

                Log($"TAPI Initialized successfully. Monitoring {addressCount} line address(es).", LocalTcpServer.LogType.Success);
                return true;
            }
            catch (Exception ex)
            {
                Log($"TAPI Initialization Failed: {ex.Message}", LocalTcpServer.LogType.Error);
                BroadcastError("Initialization Failed", ex.ToString());
                return false;
            }
        }

        public void Shutdown()
        {
            try
            {
                if (_tapiManager != null)
                {
                    if (_cookie != 0)
                    {
                        _tapiManager.UnregisterNotifications(_cookie);
                        _cookie = 0;
                    }

                    // MATCHED TO YOUR ENVIRONMENT: Using the double _Event_Event signature
                    _tapiManager.ITTAPIEventNotification_Event_Event -= OnTapiEvent;
                    _tapiManager.Shutdown();
                    Marshal.ReleaseComObject(_tapiManager);
                    _tapiManager = null;
                }
            }
            catch { }
        }

        private void OnTapiEvent(TAPI_EVENT TapiEvent, object pEvent)
        {
            try
            {
                switch (TapiEvent)
                {
                    case TAPI_EVENT.TE_CALLNOTIFICATION:
                        Log("New call notification event detected.", LocalTcpServer.LogType.Tapi);
                        break;

                    case TAPI_EVENT.TE_CALLSTATE:
                        if (pEvent is ITCallStateEvent stateEvent)
                        {
                            // SAFE CHECK: Ensure the Call object exists before probing it
                            ITCallInfo? callInfo = stateEvent.Call;
                            if (callInfo == null) return;

                            int callId = 0;
                            try { callId = callInfo.GetHashCode(); } catch { callId = new Random().Next(1000, 9999); }

                            CALL_STATE state = stateEvent.State;
                            HandleCallStateChange(callId, state, callInfo);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Critical Error in OnTapiEvent wrapper: {ex.Message}", LocalTcpServer.LogType.Error);
                BroadcastError("Event Processing Exception", ex.Message);
            }
        }

        private void HandleCallStateChange(int callId, CALL_STATE state, ITCallInfo callInfo)
        {
            // SAFE COM GETTERS: Hardware drivers throw E_FAIL if a property is blank/unsupported.
            // Wrapping each one ensures one missing detail won't break the monitoring loop.
            string callerNum = TryGetCallInfo(callInfo, CALLINFO_STRING.CIS_CALLERIDNUMBER, "Unknown");
            string callerName = TryGetCallInfo(callInfo, CALLINFO_STRING.CIS_CALLERIDNAME, "Unknown Caller");
            string calledNum = TryGetCallInfo(callInfo, CALLINFO_STRING.CIS_CALLEDIDNUMBER, "Unknown");
            string calledName = TryGetCallInfo(callInfo, CALLINFO_STRING.CIS_CALLEDIDNAME, "Main Line");

            switch (state)
            {
                case CALL_STATE.CS_OFFERING:
                    _callStartTimes[callId] = DateTime.Now;

                    var incoming = new EventWrapper
                    {
                        Type = "OnIncomingCall",
                        Data = new IncomingCallData
                        {
                            CallID = callId,
                            CallerNumber = callerNum,
                            CallerName = callerName,
                            CalledNumber = calledNum,
                            CalledName = calledName,
                            StartTime = _callStartTimes[callId].ToString("yyyy-MM-ddTHH:mm:ss")
                        }
                    };

                    Log($"Incoming Call Detected: {callerNum} ({callerName})", LocalTcpServer.LogType.Event);
                    _server.Broadcast(incoming.ToJson());
                    OnIncomingCallAlert?.Invoke("Incoming Call", $"From: {callerName}\nNumber: {callerNum}");
                    break;

                case CALL_STATE.CS_CONNECTED:
                    DateTime connectTime = DateTime.Now;
                    _callConnectedTimes[callId] = connectTime;

                    double durationBeforeAnswer = 0;
                    if (_callStartTimes.TryGetValue(callId, out var start))
                    {
                        durationBeforeAnswer = (connectTime - start).TotalSeconds;
                    }

                    var connected = new EventWrapper
                    {
                        Type = "OnCallConnected",
                        Data = new CallConnectedData
                        {
                            CallID = callId,
                            CallerNumber = callerNum,
                            ConnectedTime = connectTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                            DurationBeforeAnswer = Math.Round(durationBeforeAnswer, 2)
                        }
                    };

                    Log($"Call Connected. CallID: {callId}", LocalTcpServer.LogType.Event);
                    _server.Broadcast(connected.ToJson());
                    break;

                case CALL_STATE.CS_DISCONNECTED:
                    DateTime endTime = DateTime.Now;
                    double totalDuration = 0;

                    if (_callStartTimes.TryGetValue(callId, out var startTime))
                    {
                        totalDuration = (endTime - startTime).TotalSeconds;
                        _callStartTimes.Remove(callId);
                    }
                    _callConnectedTimes.Remove(callId);

                    var ended = new EventWrapper
                    {
                        Type = "OnCallEnded",
                        Data = new CallEndedData
                        {
                            CallID = callId,
                            CallerNumber = callerNum,
                            StartTime = startTime != default ? startTime.ToString("yyyy-MM-ddTHH:mm:ss") : "Unknown",
                            EndTime = endTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                            TotalDuration = Math.Round(totalDuration, 2)
                        }
                    };

                    Log($"Call Disconnected. Duration: {totalDuration}s", LocalTcpServer.LogType.Event);
                    _server.Broadcast(ended.ToJson());
                    break;
            }
        }

        private void BroadcastError(string message, string details)
        {
            var errorEvent = new EventWrapper
            {
                Type = "OnTapiError",
                Data = new TapiErrorData { Message = message, ErrorDetails = details }
            };
            _server.Broadcast(errorEvent.ToJson());
        }

        private void Log(string msg, LocalTcpServer.LogType type) => OnLog?.Invoke(msg, type);


        // Helper method to protect the application from raw COM field faults
        private string TryGetCallInfo(ITCallInfo callInfo, CALLINFO_STRING infoString, string fallback)
        {
            try
            {
                string val = callInfo.get_CallInfoString(infoString);
                return string.IsNullOrEmpty(val) ? fallback : val;
            }
            catch (COMException)
            {
                // Safe catch for E_FAIL exceptions thrown by unpopulated properties
                return fallback;
            }
        }

    }
}