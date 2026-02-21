using System.Net.WebSockets;
using System.Collections.Concurrent;

namespace SimpleMessenger.Services;

public class ClientManager : IClientManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();

    public void AddClient(string id, WebSocket socket) => 
        _clients[id] = socket;

    public void RemoveClient(string id) => 
        _clients.TryRemove(id, out _);

    public IReadOnlyDictionary<string, WebSocket> GetAllClients() => _clients;
    
    public WebSocket? GetClient(string id) => 
        _clients.TryGetValue(id, out var client) ? client : null;
}
