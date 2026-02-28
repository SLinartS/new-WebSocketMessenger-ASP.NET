namespace SimpleMessenger.Services;

using SimpleMessenger.Models;

public interface IMessageRepository
{
    public Task<List<ChatMessage>> GetAllAsync();
    public Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId);
    public Task AddAsync(ChatMessage message);
    public Task ClearAsync();
    public Task ClearMessagesByChatAsync(string chatRoomId);
    public Task<List<ChatRoom>> GetChatRoomsAsync();
    public Task AddChatRoomAsync(ChatRoom chatRoom);
    public Task RemoveChatRoomAsync(string chatRoomId);
    public Task<List<ChatParticipant>> GetChatParticipantsAsync();
    public Task AddChatParticipantAsync(ChatParticipant participant);
    public Task RemoveChatParticipantAsync(string chatRoomId, string userId);
    public Task<List<ChatParticipant>> GetParticipantsByChatAsync(string chatRoomId);
    public Task<bool> IsUserInChatAsync(string chatRoomId, string userId);
}
