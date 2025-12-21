using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace EncryptedMessaging.Security;

public static class PasswordHasher
{
    private const int SaltSize = 128 / 8; // 16 bytes
    private const int HashSize = 256 / 8; // 32 bytes
    private const int Iterations = 100000;

    public static string HashPassword(string password)
    {
        // Generate a random salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Hash the password
        byte[] hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashSize
        );

        // Combine salt and hash
        byte[] hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        // Convert to base64 for storage
        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string hashedPassword)
    {
        try
        {
            // Decode the stored hash
            byte[] hashBytes = Convert.FromBase64String(hashedPassword);

            // Extract the salt
            byte[] salt = new byte[SaltSize];
            Array.Copy(hashBytes, 0, salt, 0, SaltSize);

            // Hash the provided password with the same salt
            byte[] hash = KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: Iterations,
                numBytesRequested: HashSize
            );

            // Compare the hashes
            for (int i = 0; i < HashSize; i++)
            {
                if (hashBytes[i + SaltSize] != hash[i])
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}