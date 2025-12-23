using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace EncryptedMessaging.Client.security;

public static class KeyDerivation
{
    private const int Iterations = 100000;
    
    public static (byte[] PrivateKey, string PublicKeyBase64) DeriveKeyPair(string username, string password)
    {
        var seed = DeriveKeySeed(username, password);
        
        var privateKey = SHA256.HashData(seed);
        var publicKey = SHA256.HashData(privateKey);
        
        return (privateKey, Convert.ToBase64String(publicKey));
    }
    
    public static string GetPublicKeyBase64(string username, string password)
    {
        var (_, publicKeyBase64) = DeriveKeyPair(username, password);
        return publicKeyBase64;
    }
    
    public static byte[] GetPrivateKey(string username, string password)
    {
        var (privateKey, _) = DeriveKeyPair(username, password);
        return privateKey;
    }
    
    public static byte[] DeriveSharedSecret(byte[] myPrivateKey, string theirPublicKeyBase64)
    {
        var theirPublicKey = Convert.FromBase64String(theirPublicKeyBase64);
        var myPublicKey = SHA256.HashData(myPrivateKey);
        
        byte[] first, second;
        if (CompareBytes(myPublicKey, theirPublicKey) < 0)
        {
            first = myPublicKey;
            second = theirPublicKey;
        }
        else
        {
            first = theirPublicKey;
            second = myPublicKey;
        }
        
        var combined = new byte[first.Length + second.Length];
        Array.Copy(first, 0, combined, 0, first.Length);
        Array.Copy(second, 0, combined, first.Length, second.Length);
        
        return SHA256.HashData(combined);
    }
    
    private static int CompareBytes(byte[] a, byte[] b)
    {
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i])
                return a[i].CompareTo(b[i]);
        }
        return a.Length.CompareTo(b.Length);
    }
    
    private static byte[] DeriveKeySeed(string username, string password)
    {
        var salt = Encoding.UTF8.GetBytes($"EncryptedMessaging:{username}:v1");
        
        return Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: 64);
    }
    
    public static string Encrypt(string plainText, byte[] sharedSecret)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = sharedSecret;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
        Array.Copy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherText, byte[] sharedSecret)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            
            var iv = new byte[16];
            var cipher = new byte[fullCipher.Length - 16];
            Array.Copy(fullCipher, 0, iv, 0, 16);
            Array.Copy(fullCipher, 16, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            aes.Key = sharedSecret;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return "[Unable to decrypt message]";
        }
    }
}
