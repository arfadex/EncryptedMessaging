namespace EncryptedMessaging.Client.models;

public record RegisterRequest(string Username, string Password, string PublicKey);

public record LoginRequest(string Username, string Password);

public record AuthResponse(int UserId, string Username, string Token, string RefreshToken);

public record SendMessageRequest(string ReceiverUsername, string EncryptedContent);

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

public record UserResponse(int Id, string Username, string PublicKey, DateTime CreatedAt);

public record UserListResponse(List<UserResponse> Users);
