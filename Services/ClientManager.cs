
using System.Collections.Concurrent;
using System.Net.WebSockets;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class ClientManager : IClientManager
{
    private readonly ConcurrentDictionary<string, WebSocket> clients = new();
    private readonly ConcurrentDictionary<string, ActiveUser> users = new();

    public event Action<UserChangedEventArgs>? UsersChanged;

    public void AddClient(string id, WebSocket socket, string ipAddress)
    {
        clients[id] = socket;
        users[id] = new ActiveUser
        {
            Id = id,
            Nickname = "Anonymous",
            IpAddress = ipAddress,
            ConnectedAt = DateTime.UtcNow,
        };

        RaiseUsersChanged("added", users[id]);
    }

    public void RemoveClient(string id)
    {
        clients.TryRemove(id, out _);
        if (users.TryRemove(id, out ActiveUser? user))
        {
            RaiseUsersChanged("removed", user);
        }
    }

    public void UpdateUserNickname(string id, string nickname)
    {
        if (users.TryGetValue(id, out ActiveUser? user))
        {
            user.Nickname = nickname;
            RaiseUsersChanged("updated", user);
        }
    }

    public void UpdateUserTypingStatus(string id, bool isTyping)
    {
        if (users.TryGetValue(id, out ActiveUser? user))
        {
            user.IsTyping = isTyping;
            RaiseUsersChanged("updated", user);
        }
    }

    public void UpdateUserCurrentChat(string id, string chatRoomId)
    {
        if (users.TryGetValue(id, out ActiveUser? user))
        {
            user.CurrentChatId = chatRoomId;
            RaiseUsersChanged("updated", user);
        }
    }

    public IReadOnlyDictionary<string, WebSocket> GetAllClients() => clients;

    public WebSocket? GetClient(string id) =>
        clients.TryGetValue(id, out WebSocket? client) ? client : null;

    public IEnumerable<ActiveUser> GetActiveUsers() =>
        [
            .. users.Values.Select(u => new ActiveUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IpAddress = u.IpAddress,
                ConnectedAt = u.ConnectedAt,
                IsTyping = u.IsTyping,
                CurrentChatId = u.CurrentChatId,
            }),
        ];

    public ActiveUser? GetUser(string id) => users.TryGetValue(id, out ActiveUser? user) ? user : null;

    private void RaiseUsersChanged(string type, ActiveUser user) =>
        UsersChanged?.Invoke(new UserChangedEventArgs { Type = type, User = user });
}
