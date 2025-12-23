using System.Data.SQLite;
using EncryptedMessaging.Server.models;

namespace EncryptedMessaging.Server.data;

public class UserRepository
{
    public async Task<User?> CreateUserAsync(string username, string passwordHash, string publicKey)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            INSERT INTO Users (Username, PasswordHash, PublicKey, CreatedAt)
            VALUES (@Username, @PasswordHash, @PublicKey, @CreatedAt);
            SELECT last_insert_rowid();";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@Username", username);
        cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
        cmd.Parameters.AddWithValue("@PublicKey", publicKey);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));

        try
        {
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return new User
            {
                Id = id,
                Username = username,
                PasswordHash = passwordHash,
                PublicKey = publicKey,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (SQLiteException)
        {
            return null;
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = "SELECT Id, Username, PasswordHash, PublicKey, CreatedAt FROM Users WHERE Username = @Username";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@Username", username);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                PublicKey = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            };
        }

        return null;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = "SELECT Id, Username, PasswordHash, PublicKey, CreatedAt FROM Users WHERE Id = @Id";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                PublicKey = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            };
        }

        return null;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var users = new List<User>();

        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = "SELECT Id, Username, PasswordHash, PublicKey, CreatedAt FROM Users ORDER BY Username";

        using var cmd = new SQLiteCommand(query, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                PublicKey = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }

        return users;
    }

    public async Task<bool> UpdatePublicKeyAsync(int userId, string publicKey)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = "UPDATE Users SET PublicKey = @PublicKey WHERE Id = @Id";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@PublicKey", publicKey);
        cmd.Parameters.AddWithValue("@Id", userId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}
