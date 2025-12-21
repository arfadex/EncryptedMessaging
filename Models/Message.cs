namespace EncryptedMessaging.Models;

public class Message
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string EncryptedContent { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    
    // Not stored in DB - used for display
    public string SenderUsername { get; set; } = string.Empty;
    public string ReceiverUsername { get; set; } = string.Empty;
    public string DecryptedContent { get; set; } = string.Empty;
}