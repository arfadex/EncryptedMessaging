namespace EncryptedMessaging.Server.models.dtos;

public record UserResponse(int Id, string Username, string PublicKey, DateTime CreatedAt);

public record UserListResponse(List<UserResponse> Users);
