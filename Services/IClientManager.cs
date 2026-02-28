
using System.Net.WebSockets;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public interface IClientManager
{
    public event Action<UserChangedEventArgs>? UsersChanged;

    public void AddClient(string id, WebSocket socket, string ipAddress);
    public void RemoveClient(string id);
    public void UpdateUserNickname(string id, string nickname);
    public void UpdateUserTypingStatus(string id, bool isTyping);
    public void UpdateUserCurrentChat(string id, string chatRoomId);
    public IReadOnlyDictionary<string, WebSocket> GetAllClients();
    public WebSocket? GetClient(string id);
    public IEnumerable<ActiveUser> GetActiveUsers();
    public ActiveUser? GetUser(string id);
}

public class UserChangedEventArgs
{
    public string Type { get; set; } = string.Empty; // "added", "removed", "updated"
    public ActiveUser User { get; set; } = new();
}
