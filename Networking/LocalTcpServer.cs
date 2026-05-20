using System;
using System.Collections.Concurrent;
using System.Configuration; // NEW: Required assembly reference
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TapiMonitorApp.Models;

namespace TapiMonitorApp.Networking
{
    public class LocalTcpServer
    {
        private TcpListener? _listener;
        private readonly ConcurrentDictionary<Guid, TcpClient> _clients = new ConcurrentDictionary<Guid, TcpClient>();
        private CancellationTokenSource? _cts;
        private int _pingCounter = 0;
        private System.Threading.Timer? _pingTimer;

        // Configuration values read out of App.config
        private readonly int _port;
        private readonly int _maxConnections;

        public event Action<string, LogType>? OnLog;
        public int ActiveClientCount => _clients.Count;

        public enum LogType { Success, Error, Warning, Event, Tapi, Info }

        /// <summary>
        /// Initializes the server by pulling configuration values straight from App.config settings.
        /// </summary>
        public LocalTcpServer()
        {
            // 1. Parse Port Setting with a fallback default of 1471
            string portSetting = ConfigurationManager.AppSettings["TcpServerPort"];
            if (!int.TryParse(portSetting, out _port))
            {
                _port = 1471;
            }

            // 2. Parse Max Connections Setting with a fallback default of 10
            string maxConnSetting = ConfigurationManager.AppSettings["TcpMaxConnections"];
            if (!int.TryParse(maxConnSetting, out _maxConnections))
            {
                _maxConnections = 10;
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();

            Log($"TCP Server started via App.config configuration. Listening on port: {_port}. Connection cap: {_maxConnections}.", LogType.Success);

            Task.Run(() => AcceptClientsAsync(_cts.Token));
            _pingTimer = new System.Threading.Timer(SendPing, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void Stop()
        {
            _pingTimer?.Dispose();
            _cts?.Cancel();
            _listener?.Stop();

            foreach (var client in _clients.Values)
            {
                try { client.Close(); } catch { }
            }
            _clients.Clear();
            Log("TCP Server stopped gracefully.", LogType.Info);
        }

        private async Task AcceptClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_listener == null) break;
                    TcpClient client = await _listener.AcceptTcpClientAsync(token);

                    var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    if (remoteEndPoint == null)
                    {
                        client.Close();
                        continue;
                    }

                    if (_clients.Count >= _maxConnections)
                    {
                        client.Close();
                        Log($"Rejected connection from {remoteEndPoint}. Server limit of {_maxConnections} clients reached.", LogType.Warning);
                        continue;
                    }

                    if (!IPAddress.IsLoopback(remoteEndPoint.Address))
                    {
                        client.Close();
                        Log($"Blocked external network address request: {remoteEndPoint.Address}", LogType.Warning);
                        continue;
                    }

                    Guid clientId = Guid.NewGuid();
                    _clients.TryAdd(clientId, client);
                    Log($"Client connected from {remoteEndPoint}. Total active: {_clients.Count}", LogType.Info);

                    _ = Task.Run(() => MonitorClientConnection(clientId, client, token), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Log($"Error accepting client: {ex.Message}", LogType.Error);
                }
            }
        }

        private async Task MonitorClientConnection(Guid id, TcpClient client, CancellationToken token)
        {
            try
            {
                byte[] trashBuffer = new byte[1024];
                NetworkStream stream = client.GetStream();

                while (!token.IsCancellationRequested && client.Connected)
                {
                    int bytesRead = await stream.ReadAsync(trashBuffer, 0, trashBuffer.Length, token);
                    if (bytesRead == 0) break;
                }
            }
            catch { }
            finally
            {
                if (_clients.TryRemove(id, out var removedClient))
                {
                    removedClient.Close();
                    Log($"Client disconnected. Remaining: {_clients.Count}", LogType.Info);
                }
            }
        }

        public void Broadcast(string payload)
        {
            byte[] data = Encoding.UTF8.GetBytes(payload);
            foreach (var kvp in _clients)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        if (kvp.Value.Connected)
                        {
                            await kvp.Value.GetStream().WriteAsync(data, 0, data.Length);
                        }
                    }
                    catch
                    {
                        if (_clients.TryRemove(kvp.Key, out var deadClient))
                        {
                            deadClient.Close();
                            Log($"Removed unresponsive client. Active: {_clients.Count}", LogType.Warning);
                        }
                    }
                });
            }
        }

        private void SendPing(object? state)
        {
            Interlocked.Increment(ref _pingCounter);
            var pingEvent = new EventWrapper
            {
                Type = "PING",
                Data = new PingData
                {
                    PingNumber = _pingCounter,
                    ServerTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ActiveClients = _clients.Count
                }
            };

            Log($"Broadcasting PING #{_pingCounter}", LogType.Info);
            Broadcast(pingEvent.ToJson());
        }

        private void Log(string msg, LogType type) => OnLog?.Invoke(msg, type);
    }
}