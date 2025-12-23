using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.IdentityModel.Tokens;
using EncryptedMessaging.Server.data;
using EncryptedMessaging.Server.models.dtos;
using EncryptedMessaging.Server.services;

var builder = WebApplication.CreateBuilder(args);

// JWT Secret Key configuration
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrEmpty(jwtSecretKey))
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "JWT_SECRET_KEY environment variable is required in Production. " +
            "Generate a secure key with: openssl rand -base64 32");
    }
    // Development fallback with warning
    jwtSecretKey = "zQDoTGmB8FFDNstYncJ66xsOBCHEzvzPCer8DRP+Ctw=";
    Console.WriteLine("⚠️  Using development JWT key - do not use in production. Set JWT_SECRET_KEY environment variable.");
}

builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<WebSocketConnectionManager>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<MessageRepository>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "EncryptedMessaging",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "EncryptedMessagingUsers",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/ws"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

DatabaseInitializer.Initialize();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

string HashPassword(string password)
{
    byte[] salt = Encoding.UTF8.GetBytes("EncryptedMessagingStaticSalt2024");
    return Convert.ToBase64String(KeyDerivation.Pbkdf2(
        password: password,
        salt: salt,
        prf: KeyDerivationPrf.HMACSHA256,
        iterationCount: 100000,
        numBytesRequested: 256 / 8));
}

bool VerifyPassword(string password, string hash)
{
    return HashPassword(password) == hash;
}

int GetUserId(ClaimsPrincipal user)
{
    var claim = user.FindFirst(ClaimTypes.NameIdentifier);
    return claim != null ? int.Parse(claim.Value) : 0;
}

app.MapPost("/api/auth/register", async (RegisterRequest request, UserRepository userRepo, JwtService jwtService) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { Error = "Username and password are required" });

    if (request.Password.Length < 6)
        return Results.BadRequest(new { Error = "Password must be at least 6 characters" });

    if (string.IsNullOrWhiteSpace(request.PublicKey))
        return Results.BadRequest(new { Error = "Public key is required" });

    var passwordHash = HashPassword(request.Password);
    var user = await userRepo.CreateUserAsync(request.Username, passwordHash, request.PublicKey);

    if (user == null)
        return Results.Conflict(new { Error = "Username already exists" });

    var token = jwtService.GenerateAccessToken(user.Id, user.Username);
    var refreshToken = jwtService.GenerateRefreshToken();

    return Results.Ok(new AuthResponse(user.Id, user.Username, token, refreshToken));
});

app.MapPost("/api/auth/login", async (LoginRequest request, UserRepository userRepo, JwtService jwtService) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { Error = "Username and password are required" });

    var user = await userRepo.GetUserByUsernameAsync(request.Username);
    if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = jwtService.GenerateAccessToken(user.Id, user.Username);
    var refreshToken = jwtService.GenerateRefreshToken();

    return Results.Ok(new AuthResponse(user.Id, user.Username, token, refreshToken));
});

app.MapGet("/api/users", async (UserRepository userRepo) =>
{
    var users = await userRepo.GetAllUsersAsync();
    var response = users.Select(u => new UserResponse(u.Id, u.Username, u.PublicKey, u.CreatedAt)).ToList();
    return Results.Ok(new UserListResponse(response));
}).RequireAuthorization();

app.MapGet("/api/users/{username}", async (string username, UserRepository userRepo) =>
{
    var user = await userRepo.GetUserByUsernameAsync(username);
    if (user == null)
        return Results.NotFound();

    return Results.Ok(new UserResponse(user.Id, user.Username, user.PublicKey, user.CreatedAt));
}).RequireAuthorization();

app.MapPost("/api/messages", async (SendMessageRequest request, HttpContext context, UserRepository userRepo, MessageRepository messageRepo, WebSocketConnectionManager wsManager) =>
{
    var senderId = GetUserId(context.User);
    if (senderId == 0)
        return Results.Unauthorized();

    var receiver = await userRepo.GetUserByUsernameAsync(request.ReceiverUsername);
    if (receiver == null)
        return Results.NotFound(new { Error = "Receiver not found" });

    var sender = await userRepo.GetUserByIdAsync(senderId);
    var message = await messageRepo.CreateMessageAsync(senderId, receiver.Id, request.EncryptedContent);
    if (message == null)
        return Results.BadRequest(new { Error = "Failed to create message" });

    await wsManager.NotifyNewMessageAsync(receiver.Id, message.Id, sender?.Username ?? "Unknown", message.SentAt);

    return Results.Ok(new MessageResponse(
        message.Id,
        message.SenderId,
        sender?.Username ?? "Unknown",
        message.ReceiverId,
        receiver.Username,
        message.EncryptedContent,
        message.SentAt,
        message.IsRead,
        message.IsEdited
    ));
}).RequireAuthorization();

app.MapGet("/api/messages/received", async (HttpContext context, MessageRepository messageRepo) =>
{
    var userId = GetUserId(context.User);
    if (userId == 0)
        return Results.Unauthorized();

    var messages = await messageRepo.GetReceivedMessagesAsync(userId);
    var response = messages.Select(m => new MessageResponse(
        m.Id, m.SenderId, m.SenderUsername, m.ReceiverId, m.ReceiverUsername,
        m.EncryptedContent, m.SentAt, m.IsRead, m.IsEdited
    )).ToList();

    return Results.Ok(response);
}).RequireAuthorization();

app.MapGet("/api/messages/sent", async (HttpContext context, MessageRepository messageRepo) =>
{
    var userId = GetUserId(context.User);
    if (userId == 0)
        return Results.Unauthorized();

    var messages = await messageRepo.GetSentMessagesAsync(userId);
    var response = messages.Select(m => new MessageResponse(
        m.Id, m.SenderId, m.SenderUsername, m.ReceiverId, m.ReceiverUsername,
        m.EncryptedContent, m.SentAt, m.IsRead, m.IsEdited
    )).ToList();

    return Results.Ok(response);
}).RequireAuthorization();

app.MapPut("/api/messages/{id}", async (int id, UpdateMessageRequest request, HttpContext context, MessageRepository messageRepo, WebSocketConnectionManager wsManager) =>
{
    var userId = GetUserId(context.User);
    if (userId == 0)
        return Results.Unauthorized();

    var message = await messageRepo.GetMessageByIdAsync(id);
    if (message == null)
        return Results.NotFound();

    if (message.SenderId != userId)
        return Results.Forbid();

    var success = await messageRepo.UpdateMessageAsync(id, userId, request.EncryptedContent);
    if (!success)
        return Results.BadRequest(new { Error = "Failed to update message" });

    await wsManager.NotifyMessageEditedAsync(message.ReceiverId, id);

    return Results.Ok();
}).RequireAuthorization();

app.MapDelete("/api/messages/{id}", async (int id, HttpContext context, MessageRepository messageRepo, WebSocketConnectionManager wsManager) =>
{
    var userId = GetUserId(context.User);
    if (userId == 0)
        return Results.Unauthorized();

    var message = await messageRepo.GetMessageByIdAsync(id);
    if (message == null)
        return Results.NotFound();

    if (message.SenderId != userId)
        return Results.Forbid();

    var success = await messageRepo.DeleteMessageAsync(id, userId);
    if (!success)
        return Results.BadRequest(new { Error = "Failed to delete message" });

    await wsManager.NotifyMessageDeletedAsync(message.ReceiverId, id);

    return Results.Ok();
}).RequireAuthorization();

app.MapPatch("/api/messages/{id}/read", async (int id, HttpContext context, MessageRepository messageRepo, WebSocketConnectionManager wsManager) =>
{
    var userId = GetUserId(context.User);
    if (userId == 0)
        return Results.Unauthorized();

    var message = await messageRepo.GetMessageByIdAsync(id);
    if (message == null)
        return Results.NotFound();

    if (message.ReceiverId != userId)
        return Results.Forbid();

    var success = await messageRepo.MarkAsReadAsync(id, userId);
    if (!success)
        return Results.BadRequest(new { Error = "Failed to mark message as read" });

    await wsManager.NotifyMessageReadAsync(message.SenderId, id);

    return Results.Ok();
}).RequireAuthorization();

app.Map("/ws", async (HttpContext context, JwtService jwtService, WebSocketConnectionManager wsManager) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var token = context.Request.Query["access_token"].ToString();
    if (string.IsNullOrEmpty(token))
    {
        context.Response.StatusCode = 401;
        return;
    }

    var userId = jwtService.GetUserIdFromToken(token);
    if (userId == null)
    {
        context.Response.StatusCode = 401;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    await wsManager.HandleWebSocketAsync(userId.Value, socket);
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();
