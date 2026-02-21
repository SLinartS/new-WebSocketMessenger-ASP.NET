using System.Net.WebSockets;
using System.Collections.Concurrent;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public interface IClientManager
{
    event Action<UserChangedEventArgs>? UsersChanged;
    
    void AddClient(string id, WebSocket socket, string ipAddress);
    void RemoveClient(string id);
    void UpdateUserNickname(string id, string nickname);
    void UpdateUserTypingStatus(string id, bool isTyping);
    IReadOnlyDictionary<string, WebSocket> GetAllClients();
    WebSocket? GetClient(string id);
    IEnumerable<ActiveUser> GetActiveUsers();
}

public class UserChangedEventArgs
{
    public string Type { get; set; } = string.Empty; // "added", "removed", "updated"
    public ActiveUser User { get; set; } = new();
}
