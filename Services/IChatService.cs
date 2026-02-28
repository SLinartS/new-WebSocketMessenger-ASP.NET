
using System.Net.WebSockets;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public interface IChatService
{
    public Task SendMessageAsync(string clientId, ChatMessage message);
    public Task BroadcastAsync(ChatMessage message, string? excludeClientId = null);
    public Task HandleClientAsync(
        string clientId,
        WebSocket socket,
        string ipAddress,
        CancellationToken ct
    );
    public Task<List<ChatMessage>> GetMessageHistoryAsync();
    public Task ClearChatAsync();
    public Task BroadcastUsersListAsync();
    public Task BroadcastTypingStatusAsync(string userId, string nickname, bool isTyping);
    public Task BroadcastChatUpdateAsync(
        string chatRoomId,
        List<ChatMessage> messages,
        List<OnlineUser> onlineUsers
    );
    public Task<List<OnlineUser>> GetOnlineUsersByChatAsync(string chatRoomId);
    public Task BroadcastToChatAsync(
        string chatRoomId,
        ChatMessage message,
        string? excludeClientId = null
    );
    public Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId);
}
