using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public interface IMessageRepository
{
    Task<List<ChatMessage>> GetAllAsync();
    Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId);
    Task AddAsync(ChatMessage message);
    Task ClearAsync();
    Task ClearMessagesByChatAsync(string chatRoomId);
    Task<List<ChatRoom>> GetChatRoomsAsync();
    Task AddChatRoomAsync(ChatRoom chatRoom);
    Task RemoveChatRoomAsync(string chatRoomId);
    Task<List<ChatParticipant>> GetChatParticipantsAsync();
    Task AddChatParticipantAsync(ChatParticipant participant);
    Task RemoveChatParticipantAsync(string chatRoomId, string userId);
    Task<List<ChatParticipant>> GetParticipantsByChatAsync(string chatRoomId);
    Task<bool> IsUserInChatAsync(string chatRoomId, string userId);
}
