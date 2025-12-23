using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace EncryptedMessaging.Client.services;

public class WebSocketClient : IDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    
    public event Action<string, JsonElement>? OnMessageReceived;
    public event Action? OnDisconnected;
    
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public async Task<bool> ConnectAsync(string token)
    {
        try
        {
            _socket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            var wsUrl = $"{Config.WsUrl}?access_token={Uri.EscapeDataString(token)}";
            await _socket.ConnectAsync(new Uri(wsUrl), _cancellationTokenSource.Token);
            
            _receiveTask = ReceiveLoopAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[4096];
        
        try
        {
            while (_socket?.State == WebSocketState.Open && _cancellationTokenSource?.IsCancellationRequested == false)
            {
                var result = await _socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    _cancellationTokenSource.Token
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(json);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("Type", out var typeElement) || 
                root.TryGetProperty("type", out typeElement))
            {
                var messageType = typeElement.GetString() ?? "";
                
                if (root.TryGetProperty("Payload", out var payload) ||
                    root.TryGetProperty("payload", out payload))
                {
                    OnMessageReceived?.Invoke(messageType, payload.Clone());
                }
            }
        }
        catch
        {
        }
    }

    public async Task DisconnectAsync()
    {
        _cancellationTokenSource?.Cancel();
        
        if (_socket?.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "User disconnected", 
                    CancellationToken.None
                );
            }
            catch
            {
            }
        }
        
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _socket?.Dispose();
    }
}
