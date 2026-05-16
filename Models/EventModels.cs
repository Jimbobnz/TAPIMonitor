using System;
using System.Text.Json;

namespace TapiMonitorApp.Models
{
    public class EventWrapper
    {
        public string Type { get; set; } = string.Empty;
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
        public object Data { get; set; } = new(); // <--- THE ERROR

        public string ToJson() => JsonSerializer.Serialize(this) + "\r";
    }

    public class IncomingCallData
    {
        public int CallID { get; set; }
        public string CallerNumber { get; set; } = "Unknown";
        public string CallerName { get; set; } = "Unknown Caller";
        public string CalledNumber { get; set; } = "Unknown";
        public string CalledName { get; set; } = "Main Line";
        public string StartTime { get; set; } = string.Empty;
    }

    public class CallConnectedData
    {
        public int CallID { get; set; }
        public string CallerNumber { get; set; } = "Unknown";
        public string ConnectedTime { get; set; } = string.Empty;
        public double DurationBeforeAnswer { get; set; } // In seconds
    }

    public class CallEndedData
    {
        public int CallID { get; set; }
        public string CallerNumber { get; set; } = "Unknown";
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public double TotalDuration { get; set; } // In seconds
    }

    public class TapiErrorData
    {
        public string Message { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
    }

    public class PingData
    {
        public int PingNumber { get; set; }
        public string ServerTime { get; set; } = string.Empty;
        public int ActiveClients { get; set; }
    }
}
