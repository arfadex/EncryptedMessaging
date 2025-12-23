using System.Data.SQLite;

namespace EncryptedMessaging.Server.data;

public static class DatabaseInitializer
{
    private static readonly string DatabaseFile = Environment.GetEnvironmentVariable("DATABASE_PATH") ?? "data/messages.db";
    public static string ConnectionString => $"Data Source={DatabaseFile};Version=3;";

    public static void Initialize()
    {
        var directory = Path.GetDirectoryName(DatabaseFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(DatabaseFile))
        {
            SQLiteConnection.CreateFile(DatabaseFile);
        }

        using var conn = new SQLiteConnection(ConnectionString);
        conn.Open();

        var createUsersTable = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT UNIQUE NOT NULL,
                PasswordHash TEXT NOT NULL,
                PublicKey TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )";

        var createMessagesTable = @"
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SenderId INTEGER NOT NULL,
                ReceiverId INTEGER NOT NULL,
                EncryptedContent TEXT NOT NULL,
                SentAt TEXT NOT NULL,
                IsRead INTEGER DEFAULT 0,
                IsEdited INTEGER DEFAULT 0,
                FOREIGN KEY (SenderId) REFERENCES Users(Id),
                FOREIGN KEY (ReceiverId) REFERENCES Users(Id)
            )";

        var createIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_messages_sender ON Messages(SenderId);
            CREATE INDEX IF NOT EXISTS idx_messages_receiver ON Messages(ReceiverId);
            CREATE INDEX IF NOT EXISTS idx_messages_sent_at ON Messages(SentAt);
            CREATE INDEX IF NOT EXISTS idx_users_username ON Users(Username);
        ";

        using (var cmd = new SQLiteCommand(createUsersTable, conn))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = new SQLiteCommand(createMessagesTable, conn))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = new SQLiteCommand(createIndexes, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }
}
