using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using EncryptedMessaging.Server.models.dtos;

namespace EncryptedMessaging.Server.services;

public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<int, WebSocket> _connections = new();

    public void AddConnection(int userId, WebSocket socket)
    {
        _connections.TryRemove(userId, out var oldSocket);
        if (oldSocket != null && oldSocket.State == WebSocketState.Open)
        {
            oldSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "New connection established", CancellationToken.None);
        }
        _connections.TryAdd(userId, socket);
    }

    public void RemoveConnection(int userId)
    {
        _connections.TryRemove(userId, out _);
    }

    public async Task SendToUserAsync(int userId, object message)
    {
        if (_connections.TryGetValue(userId, out var socket) && socket.State == WebSocketState.Open)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public async Task NotifyNewMessageAsync(int receiverId, int messageId, string senderUsername, DateTime sentAt)
    {
        var notification = new WebSocketMessage(
            "new_message",
            new NewMessageNotification(messageId, senderUsername, sentAt)
        );
        await SendToUserAsync(receiverId, notification);
    }

    public async Task NotifyMessageReadAsync(int senderId, int messageId)
    {
        var notification = new WebSocketMessage("message_read", new { MessageId = messageId });
        await SendToUserAsync(senderId, notification);
    }

    public async Task NotifyMessageDeletedAsync(int receiverId, int messageId)
    {
        var notification = new WebSocketMessage("message_deleted", new { MessageId = messageId });
        await SendToUserAsync(receiverId, notification);
    }

    public async Task NotifyMessageEditedAsync(int receiverId, int messageId)
    {
        var notification = new WebSocketMessage("message_edited", new { MessageId = messageId });
        await SendToUserAsync(receiverId, notification);
    }

    public bool IsUserConnected(int userId)
    {
        return _connections.TryGetValue(userId, out var socket) && socket.State == WebSocketState.Open;
    }

    public async Task HandleWebSocketAsync(int userId, WebSocket socket)
    {
        AddConnection(userId, socket);
        var buffer = new byte[1024 * 4];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        finally
        {
            RemoveConnection(userId);
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }
    }
}
