using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using EncryptedMessaging.Models;

namespace EncryptedMessaging.Data;

public class UserRepository
{
    public async Task<User?> CreateUserAsync(string username, string passwordHash)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                INSERT INTO Users (Username, PasswordHash, CreatedAt)
                VALUES (@Username, @PasswordHash, @CreatedAt);
                SELECT last_insert_rowid();";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("o"));

                try
                {
                    var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return new User
                    {
                        Id = id,
                        Username = username,
                        PasswordHash = passwordHash,
                        CreatedAt = DateTime.UtcNow
                    };
                }
                catch (SQLiteException)
                {
                    return null; // Username already exists
                }
            }
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = "SELECT Id, Username, PasswordHash, CreatedAt FROM Users WHERE Username = @Username";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Username", username);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new User
                        {
                            Id = reader.GetInt32(0),
                            Username = reader.GetString(1),
                            PasswordHash = reader.GetString(2),
                            CreatedAt = DateTime.Parse(reader.GetString(3))
                        };
                    }
                }
            }
        }

        return null;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = "SELECT Id, Username, PasswordHash, CreatedAt FROM Users WHERE Id = @Id";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new User
                        {
                            Id = reader.GetInt32(0),
                            Username = reader.GetString(1),
                            PasswordHash = reader.GetString(2),
                            CreatedAt = DateTime.Parse(reader.GetString(3))
                        };
                    }
                }
            }
        }

        return null;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        var users = new List<User>();

        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = "SELECT Id, Username, PasswordHash, CreatedAt FROM Users ORDER BY Username";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(new User
                        {
                            Id = reader.GetInt32(0),
                            Username = reader.GetString(1),
                            PasswordHash = reader.GetString(2),
                            CreatedAt = DateTime.Parse(reader.GetString(3))
                        });
                    }
                }
            }
        }

        return users;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = "DELETE FROM Users WHERE Id = @Id";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", userId);
                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> UpdateUserAsync(int userId, string newUsername, string newPasswordHash)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                UPDATE Users 
                SET Username = @Username, PasswordHash = @PasswordHash
                WHERE Id = @Id";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Username", newUsername);
                cmd.Parameters.AddWithValue("@PasswordHash", newPasswordHash);
                cmd.Parameters.AddWithValue("@Id", userId);

                try
                {
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
                catch (SQLiteException)
                {
                    return false; // Username already exists
                }
            }
        }
    }
}