using System.Data.SQLite;
using EncryptedMessaging.Server.models;

namespace EncryptedMessaging.Server.data;

public class MessageRepository
{
    public async Task<Message?> CreateMessageAsync(int senderId, int receiverId, string encryptedContent)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            INSERT INTO Messages (SenderId, ReceiverId, EncryptedContent, SentAt, IsRead, IsEdited)
            VALUES (@SenderId, @ReceiverId, @EncryptedContent, @SentAt, 0, 0);
            SELECT last_insert_rowid();";

        using var cmd = new SQLiteCommand(query, conn);
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
            SentAt = DateTime.UtcNow,
            IsRead = false,
            IsEdited = false
        };
    }

    public async Task<List<Message>> GetReceivedMessagesAsync(int userId)
    {
        var messages = new List<Message>();

        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            SELECT m.Id, m.SenderId, m.ReceiverId, m.EncryptedContent, m.SentAt, m.IsRead, m.IsEdited,
                   u.Username as SenderUsername
            FROM Messages m
            JOIN Users u ON m.SenderId = u.Id
            WHERE m.ReceiverId = @UserId
            ORDER BY m.SentAt DESC";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new Message
            {
                Id = reader.GetInt32(0),
                SenderId = reader.GetInt32(1),
                ReceiverId = reader.GetInt32(2),
                EncryptedContent = reader.GetString(3),
                SentAt = DateTime.Parse(reader.GetString(4)),
                IsRead = reader.GetInt32(5) == 1,
                IsEdited = reader.GetInt32(6) == 1,
                SenderUsername = reader.GetString(7)
            });
        }

        return messages;
    }

    public async Task<List<Message>> GetSentMessagesAsync(int userId)
    {
        var messages = new List<Message>();

        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            SELECT m.Id, m.SenderId, m.ReceiverId, m.EncryptedContent, m.SentAt, m.IsRead, m.IsEdited,
                   u.Username as ReceiverUsername
            FROM Messages m
            JOIN Users u ON m.ReceiverId = u.Id
            WHERE m.SenderId = @UserId
            ORDER BY m.SentAt DESC";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(new Message
            {
                Id = reader.GetInt32(0),
                SenderId = reader.GetInt32(1),
                ReceiverId = reader.GetInt32(2),
                EncryptedContent = reader.GetString(3),
                SentAt = DateTime.Parse(reader.GetString(4)),
                IsRead = reader.GetInt32(5) == 1,
                IsEdited = reader.GetInt32(6) == 1,
                ReceiverUsername = reader.GetString(7)
            });
        }

        return messages;
    }

    public async Task<Message?> GetMessageByIdAsync(int messageId)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            SELECT m.Id, m.SenderId, m.ReceiverId, m.EncryptedContent, m.SentAt, m.IsRead, m.IsEdited,
                   s.Username as SenderUsername, r.Username as ReceiverUsername
            FROM Messages m
            JOIN Users s ON m.SenderId = s.Id
            JOIN Users r ON m.ReceiverId = r.Id
            WHERE m.Id = @MessageId";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@MessageId", messageId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Message
            {
                Id = reader.GetInt32(0),
                SenderId = reader.GetInt32(1),
                ReceiverId = reader.GetInt32(2),
                EncryptedContent = reader.GetString(3),
                SentAt = DateTime.Parse(reader.GetString(4)),
                IsRead = reader.GetInt32(5) == 1,
                IsEdited = reader.GetInt32(6) == 1,
                SenderUsername = reader.GetString(7),
                ReceiverUsername = reader.GetString(8)
            };
        }

        return null;
    }

    public async Task<bool> UpdateMessageAsync(int messageId, int senderId, string encryptedContent)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            UPDATE Messages 
            SET EncryptedContent = @EncryptedContent, IsEdited = 1
            WHERE Id = @Id AND SenderId = @SenderId";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@EncryptedContent", encryptedContent);
        cmd.Parameters.AddWithValue("@Id", messageId);
        cmd.Parameters.AddWithValue("@SenderId", senderId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteMessageAsync(int messageId, int senderId)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = "DELETE FROM Messages WHERE Id = @Id AND SenderId = @SenderId";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", messageId);
        cmd.Parameters.AddWithValue("@SenderId", senderId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> MarkAsReadAsync(int messageId, int receiverId)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = @"
            UPDATE Messages 
            SET IsRead = 1
            WHERE Id = @Id AND ReceiverId = @ReceiverId";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", messageId);
        cmd.Parameters.AddWithValue("@ReceiverId", receiverId);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        using var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();

        const string query = "SELECT COUNT(*) FROM Messages WHERE ReceiverId = @UserId AND IsRead = 0";

        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
