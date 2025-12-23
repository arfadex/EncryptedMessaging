using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EncryptedMessaging.Client.services;

public class SessionManager
{
    private static readonly string SessionDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EncryptedMessaging"
    );
    private static readonly string SessionFile = Path.Combine(SessionDir, "session.json");
    
    private static readonly byte[] MachineKey = DeriveMachineKey();

    public record SessionData(
        int UserId,
        string Username,
        string Token,
        string RefreshToken,
        string EncryptedPrivateKey,
        DateTime SavedAt
    );

    private static byte[] DeriveMachineKey()
    {
        var machineId = $"{Environment.MachineName}-{Environment.UserName}-EncryptedMessaging";
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
    }

    public static void SaveSession(int userId, string username, string token, string refreshToken, byte[] privateKey)
    {
        try
        {
            Directory.CreateDirectory(SessionDir);

            var encryptedPrivateKey = EncryptPrivateKey(privateKey);

            var session = new SessionData(
                userId,
                username,
                token,
                refreshToken,
                encryptedPrivateKey,
                DateTime.UtcNow
            );

            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SessionFile, json);
        }
        catch
        {
        }
    }

    public static (SessionData? Session, byte[]? PrivateKey) LoadSession()
    {
        try
        {
            if (!File.Exists(SessionFile))
                return (null, null);

            var json = File.ReadAllText(SessionFile);
            var session = JsonSerializer.Deserialize<SessionData>(json);

            if (session == null)
                return (null, null);

            var privateKey = DecryptPrivateKey(session.EncryptedPrivateKey);
            if (privateKey == null)
                return (null, null);

            return (session, privateKey);
        }
        catch
        {
            return (null, null);
        }
    }

    public static void ClearSession()
    {
        try
        {
            if (File.Exists(SessionFile))
                File.Delete(SessionFile);
        }
        catch
        {
        }
    }

    public static bool HasSession()
    {
        return File.Exists(SessionFile);
    }

    private static string EncryptPrivateKey(byte[] privateKey)
    {
        using var aes = Aes.Create();
        aes.Key = MachineKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(privateKey, 0, privateKey.Length);

        var result = new byte[aes.IV.Length + encrypted.Length];
        aes.IV.CopyTo(result, 0);
        encrypted.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    private static byte[]? DecryptPrivateKey(string encryptedBase64)
    {
        try
        {
            var data = Convert.FromBase64String(encryptedBase64);

            using var aes = Aes.Create();
            aes.Key = MachineKey;

            var iv = new byte[16];
            var encrypted = new byte[data.Length - 16];
            Array.Copy(data, 0, iv, 0, 16);
            Array.Copy(data, 16, encrypted, 0, encrypted.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        }
        catch
        {
            return null;
        }
    }
}
