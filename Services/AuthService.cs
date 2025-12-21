using System.Threading.Tasks;
using EncryptedMessaging.Data;
using EncryptedMessaging.Models;
using EncryptedMessaging.Security;

namespace EncryptedMessaging.Services;

public class AuthService
{
    private readonly UserRepository _userRepository;

    public AuthService()
    {
        _userRepository = new UserRepository();
    }

    public async Task<User?> RegisterAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        if (password.Length < 6)
            return null;

        string passwordHash = PasswordHasher.HashPassword(password);
        return await _userRepository.CreateUserAsync(username, passwordHash);
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await _userRepository.GetUserByUsernameAsync(username);

        if (user == null)
            return null;

        bool isValid = PasswordHasher.VerifyPassword(password, user.PasswordHash);
        return isValid ? user : null;
    }
}