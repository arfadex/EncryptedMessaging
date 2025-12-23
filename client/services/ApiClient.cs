using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EncryptedMessaging.Client.models;

namespace EncryptedMessaging.Client.services;

public class ApiClient
{
    private HttpClient _httpClient;
    private string? _token;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(Config.ServerUrl)
        };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void UpdateBaseAddress()
    {
        var token = _token;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(Config.ServerUrl)
        };
        if (token != null)
            SetToken(token);
    }

    public void SetToken(string token)
    {
        _token = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public string? GetToken() => _token;

    public void ClearToken()
    {
        _token = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<AuthResponse?> RegisterAsync(string username, string password, string publicKey)
    {
        var request = new RegisterRequest(username, password, publicKey);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
    }

    public async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        var request = new LoginRequest(username, password);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
    }

    public async Task<List<UserResponse>> GetUsersAsync()
    {
        var response = await _httpClient.GetAsync("/api/users");
        
        if (!response.IsSuccessStatusCode)
            return new List<UserResponse>();

        var result = await response.Content.ReadFromJsonAsync<UserListResponse>(_jsonOptions);
        return result?.Users ?? new List<UserResponse>();
    }

    public async Task<UserResponse?> GetUserAsync(string username)
    {
        var response = await _httpClient.GetAsync($"/api/users/{Uri.EscapeDataString(username)}");
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserResponse>(_jsonOptions);
    }

    public async Task<MessageResponse?> SendMessageAsync(string receiverUsername, string encryptedContent)
    {
        var request = new SendMessageRequest(receiverUsername, encryptedContent);
        var response = await _httpClient.PostAsJsonAsync("/api/messages", request);
        
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<MessageResponse>(_jsonOptions);
    }

    public async Task<List<MessageResponse>> GetReceivedMessagesAsync()
    {
        var response = await _httpClient.GetAsync("/api/messages/received");
        
        if (!response.IsSuccessStatusCode)
            return new List<MessageResponse>();

        return await response.Content.ReadFromJsonAsync<List<MessageResponse>>(_jsonOptions) ?? new List<MessageResponse>();
    }

    public async Task<List<MessageResponse>> GetSentMessagesAsync()
    {
        var response = await _httpClient.GetAsync("/api/messages/sent");
        
        if (!response.IsSuccessStatusCode)
            return new List<MessageResponse>();

        return await response.Content.ReadFromJsonAsync<List<MessageResponse>>(_jsonOptions) ?? new List<MessageResponse>();
    }

    public async Task<bool> MarkMessageAsReadAsync(int messageId)
    {
        var response = await _httpClient.PatchAsync($"/api/messages/{messageId}/read", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
