using System.Collections.Generic;
using System.Threading.Tasks;
using EncryptedMessaging.Data;
using EncryptedMessaging.Models;

namespace EncryptedMessaging.Services;

public class MessageService
{
    private readonly MessageRepository _messageRepository;
    private readonly UserRepository _userRepository;

    public MessageService()
    {
        _messageRepository = new MessageRepository();
        _userRepository = new UserRepository();
    }

    public async Task<Message?> SendMessageAsync(int senderId, string receiverUsername, string content)
    {
        var receiver = await _userRepository.GetUserByUsernameAsync(receiverUsername);
        
        if (receiver == null)
            return null;

        return await _messageRepository.CreateMessageAsync(senderId, receiver.Id, content);
    }

    public async Task<List<Message>> GetReceivedMessagesAsync(int userId)
    {
        return await _messageRepository.GetReceivedMessagesAsync(userId);
    }

    public async Task<List<Message>> GetSentMessagesAsync(int userId)
    {
        return await _messageRepository.GetSentMessagesAsync(userId);
    }

    public async Task<bool> UpdateMessageAsync(int messageId, int senderId, string newContent)
    {
        return await _messageRepository.UpdateMessageAsync(messageId, senderId, newContent);
    }

    public async Task<bool> DeleteMessageAsync(int messageId, int senderId)
    {
        return await _messageRepository.DeleteMessageAsync(messageId, senderId);
    }

    public async Task<bool> MarkAsReadAsync(int messageId, int receiverId)
    {
        return await _messageRepository.MarkAsReadAsync(messageId, receiverId);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllUsersAsync();
    }
}