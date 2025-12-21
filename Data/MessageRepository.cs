using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using EncryptedMessaging.Models;
using EncryptedMessaging.Security;

namespace EncryptedMessaging.Data;

public class MessageRepository
{
    public async Task<Message?> CreateMessageAsync(int senderId, int receiverId, string content)
    {
        string encryptedContent = AesEncryption.Encrypt(content);

        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                INSERT INTO Messages (SenderId, ReceiverId, EncryptedContent, SentAt, IsRead)
                VALUES (@SenderId, @ReceiverId, @EncryptedContent, @SentAt, 0);
                SELECT last_insert_rowid();";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@SenderId", senderId);
                cmd.Parameters.AddWithValue("@ReceiverId", receiverId);
                cmd.Parameters.AddWithValue("@EncryptedContent", encryptedContent);
                cmd.Parameters.AddWithValue("@SentAt", DateTime.UtcNow.ToString("o"));

                var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return new Message
                {
                    Id = id,
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    EncryptedContent = encryptedContent,
                    DecryptedContent = content,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };
            }
        }
    }

    public async Task<List<Message>> GetReceivedMessagesAsync(int userId)
    {
        var messages = new List<Message>();

        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                SELECT m.Id, m.SenderId, m.ReceiverId, m.EncryptedContent, m.SentAt, m.IsRead,
                       u.Username as SenderUsername
                FROM Messages m
                JOIN Users u ON m.SenderId = u.Id
                WHERE m.ReceiverId = @UserId
                ORDER BY m.SentAt DESC";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string encryptedContent = reader.GetString(3);
                        messages.Add(new Message
                        {
                            Id = reader.GetInt32(0),
                            SenderId = reader.GetInt32(1),
                            ReceiverId = reader.GetInt32(2),
                            EncryptedContent = encryptedContent,
                            DecryptedContent = AesEncryption.Decrypt(encryptedContent),
                            SentAt = DateTime.Parse(reader.GetString(4)),
                            IsRead = reader.GetInt32(5) == 1,
                            SenderUsername = reader.GetString(6)
                        });
                    }
                }
            }
        }

        return messages;
    }

    public async Task<List<Message>> GetSentMessagesAsync(int userId)
    {
        var messages = new List<Message>();

        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                SELECT m.Id, m.SenderId, m.ReceiverId, m.EncryptedContent, m.SentAt, m.IsRead,
                       u.Username as ReceiverUsername
                FROM Messages m
                JOIN Users u ON m.ReceiverId = u.Id
                WHERE m.SenderId = @UserId
                ORDER BY m.SentAt DESC";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string encryptedContent = reader.GetString(3);
                        messages.Add(new Message
                        {
                            Id = reader.GetInt32(0),
                            SenderId = reader.GetInt32(1),
                            ReceiverId = reader.GetInt32(2),
                            EncryptedContent = encryptedContent,
                            DecryptedContent = AesEncryption.Decrypt(encryptedContent),
                            SentAt = DateTime.Parse(reader.GetString(4)),
                            IsRead = reader.GetInt32(5) == 1,
                            ReceiverUsername = reader.GetString(6)
                        });
                    }
                }
            }
        }

        return messages;
    }

    public async Task<bool> UpdateMessageAsync(int messageId, int senderId, string newContent)
    {
        string encryptedContent = AesEncryption.Encrypt(newContent);

        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                UPDATE Messages 
                SET EncryptedContent = @EncryptedContent
                WHERE Id = @Id AND SenderId = @SenderId";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@EncryptedContent", encryptedContent);
                cmd.Parameters.AddWithValue("@Id", messageId);
                cmd.Parameters.AddWithValue("@SenderId", senderId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> DeleteMessageAsync(int messageId, int senderId)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = "DELETE FROM Messages WHERE Id = @Id AND SenderId = @SenderId";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", messageId);
                cmd.Parameters.AddWithValue("@SenderId", senderId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<bool> MarkAsReadAsync(int messageId, int receiverId)
    {
        using (var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString))
        {
            await conn.OpenAsync();

            string query = @"
                UPDATE Messages 
                SET IsRead = 1
                WHERE Id = @Id AND ReceiverId = @ReceiverId";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", messageId);
                cmd.Parameters.AddWithValue("@ReceiverId", receiverId);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }
}