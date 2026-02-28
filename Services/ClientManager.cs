namespace SimpleMessenger.Services;

using System.Collections.Concurrent;
using System.Net.WebSockets;
using SimpleMessenger.Models;

public class ClientManager : IClientManager
{
    private readonly ConcurrentDictionary<string, WebSocket> clients = new();
    private readonly ConcurrentDictionary<string, ActiveUser> users = new();

    public event Action<UserChangedEventArgs>? UsersChanged;

    public void AddClient(string id, WebSocket socket, string ipAddress)
    {
        this.clients[id] = socket;
        this.users[id] = new ActiveUser
        {
            Id = id,
            Nickname = "Anonymous",
            IpAddress = ipAddress,
            ConnectedAt = DateTime.UtcNow,
        };

        this.RaiseUsersChanged("added", this.users[id]);
    }

    public void RemoveClient(string id)
    {
        this.clients.TryRemove(id, out _);
        if (this.users.TryRemove(id, out var user))
        {
            this.RaiseUsersChanged("removed", user);
        }
    }

    public void UpdateUserNickname(string id, string nickname)
    {
        if (this.users.TryGetValue(id, out var user))
        {
            user.Nickname = nickname;
            this.RaiseUsersChanged("updated", user);
        }
    }

    public void UpdateUserTypingStatus(string id, bool isTyping)
    {
        if (this.users.TryGetValue(id, out var user))
        {
            user.IsTyping = isTyping;
            this.RaiseUsersChanged("updated", user);
        }
    }

    public void UpdateUserCurrentChat(string id, string chatRoomId)
    {
        if (this.users.TryGetValue(id, out var user))
        {
            user.CurrentChatId = chatRoomId;
            this.RaiseUsersChanged("updated", user);
        }
    }

    public IReadOnlyDictionary<string, WebSocket> GetAllClients() => this.clients;

    public WebSocket? GetClient(string id) =>
        this.clients.TryGetValue(id, out var client) ? client : null;

    public IEnumerable<ActiveUser> GetActiveUsers() =>
        [
            .. this.users.Values.Select(u => new ActiveUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IpAddress = u.IpAddress,
                ConnectedAt = u.ConnectedAt,
                IsTyping = u.IsTyping,
                CurrentChatId = u.CurrentChatId,
            }),
        ];

    public ActiveUser? GetUser(string id) => this.users.TryGetValue(id, out var user) ? user : null;

    private void RaiseUsersChanged(string type, ActiveUser user) =>
        UsersChanged?.Invoke(new UserChangedEventArgs { Type = type, User = user });
}
