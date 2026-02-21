using System.Net.WebSockets;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public interface IChatService
{
    Task SendMessageAsync(string clientId, ChatMessage message);
    Task BroadcastAsync(ChatMessage message, string? excludeClientId = null);
    Task HandleClientAsync(string clientId, WebSocket socket, string ipAddress, CancellationToken ct);
    Task<List<ChatMessage>> GetMessageHistoryAsync();
    Task ClearChatAsync();
    Task BroadcastUsersListAsync();
}
