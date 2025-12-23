namespace EncryptedMessaging.Server.models.dtos;

public record SendMessageRequest(string ReceiverUsername, string EncryptedContent);

public record UpdateMessageRequest(string EncryptedContent);

public record MessageResponse(
    int Id,
    int SenderId,
    string SenderUsername,
    int ReceiverId,
    string ReceiverUsername,
    string EncryptedContent,
    DateTime SentAt,
    bool IsRead,
    bool IsEdited
);

public record WebSocketMessage(string Type, object Payload);

public record NewMessageNotification(int MessageId, string SenderUsername, DateTime SentAt);
