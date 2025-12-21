using System.Data.SQLite;
using System.IO;

namespace EncryptedMessaging.Data;

public static class DatabaseInitializer
{
    public const string ConnectionString = "Data Source=messaging.db;Version=3;";

    public static void Initialize()
    {
        bool dbExists = File.Exists("messaging.db");

        if (!dbExists)
        {
            SQLiteConnection.CreateFile("messaging.db");
        }

        using (var conn = new SQLiteConnection(ConnectionString))
        {
            conn.Open();

            string createUsersTable = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                )";

            string createMessagesTable = @"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SenderId INTEGER NOT NULL,
                    ReceiverId INTEGER NOT NULL,
                    EncryptedContent TEXT NOT NULL,
                    SentAt TEXT NOT NULL,
                    IsRead INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY (SenderId) REFERENCES Users(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ReceiverId) REFERENCES Users(Id) ON DELETE CASCADE
                )";

            string createIndexes = @"
                CREATE INDEX IF NOT EXISTS idx_messages_receiver ON Messages(ReceiverId);
                CREATE INDEX IF NOT EXISTS idx_messages_sender ON Messages(SenderId);
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
}