using System;
using System.Collections.Concurrent;
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
        private readonly int _port = 1471;
        private TcpListener? _listener;
        private readonly ConcurrentDictionary<Guid, TcpClient> _clients = new();
        private CancellationTokenSource? _cts;
        private int _pingCounter = 0;
        private System.Threading.Timer? _pingTimer;

        public event Action<string, LogType>? OnLog;
        public int ActiveClientCount => _clients.Count;

        public enum LogType { Success, Error, Warning, Event, Tapi, Info }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, _port); // Localhost only
            _listener.Start();
            
            Log("TCP Server started on localhost:1471", LogType.Success);

            Task.Run(() => AcceptClientsAsync(_cts.Token));
            
            // Setup 30-second heartbeat ping
            _pingTimer = new System.Threading.Timer(SendPing, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void Stop()
        {
            _pingTimer?.Dispose();
            _cts?.Cancel();
            _listener?.Stop();

            foreach (var client in _clients.Values)
            {
                client.Close();
            }
            _clients.Clear();
            Log("TCP Server stopped.", LogType.Info);
        }

        private async Task AcceptClientsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_listener == null) break;
                    TcpClient client = await _listener.AcceptTcpClientAsync(token);
                    Guid clientId = Guid.NewGuid();
                    _clients.TryAdd(clientId, client);
                    
                    Log($"Client connected from {client.Client.RemoteEndPoint}. Total: {_clients.Count}", LogType.Info);
                    
                    // Handle retention/disconnection monitoring per client asynchronously
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
                byte[] buffer = new byte[1024];
                NetworkStream stream = client.GetStream();
                // Keep reading to detect client disconnection
                while (!token.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0) break; // Client disconnected
                }
            }
            catch { }
            finally
            {
                _clients.TryRemove(id, out _);
                client.Close();
                Log($"Client disconnected. Remaining: {_clients.Count}", LogType.Info);
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
                            Log($"Removed unresponsive client. Total: {_clients.Count}", LogType.Warning);
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
