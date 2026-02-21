using System.Net.WebSockets;
using System.Collections.Concurrent;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class ClientManager : IClientManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ConcurrentDictionary<string, ActiveUser> _users = new();

    public event Action<UserChangedEventArgs>? UsersChanged;

    public void AddClient(string id, WebSocket socket, string ipAddress)
    {
        _clients[id] = socket;
        _users[id] = new ActiveUser
        {
            Id = id,
            Nickname = "Anonymous",
            IpAddress = ipAddress,
            ConnectedAt = DateTime.UtcNow
        };
        
        RaiseUsersChanged("added", _users[id]);
    }

    public void RemoveClient(string id)
    {
        _clients.TryRemove(id, out _);
        if (_users.TryRemove(id, out var user))
        {
            RaiseUsersChanged("removed", user);
        }
    }

    public void UpdateUserNickname(string id, string nickname)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.Nickname = nickname;
            RaiseUsersChanged("updated", user);
        }
    }

    public void UpdateUserTypingStatus(string id, bool isTyping)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.IsTyping = isTyping;
            RaiseUsersChanged("updated", user);
        }
    }

    public IReadOnlyDictionary<string, WebSocket> GetAllClients() => _clients;
    
    public WebSocket? GetClient(string id) => 
        _clients.TryGetValue(id, out var client) ? client : null;
    
    public IEnumerable<ActiveUser> GetActiveUsers() => _users.Values.ToList();

    private void RaiseUsersChanged(string type, ActiveUser user)
    {
        UsersChanged?.Invoke(new UserChangedEventArgs
        {
            Type = type,
            User = user
        });
    }
}
