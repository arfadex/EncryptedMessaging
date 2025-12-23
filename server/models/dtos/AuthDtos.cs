namespace EncryptedMessaging.Server.models.dtos;

public record RegisterRequest(string Username, string Password, string PublicKey);

public record LoginRequest(string Username, string Password);

public record AuthResponse(int UserId, string Username, string Token, string RefreshToken);

public record RefreshRequest(string RefreshToken);
