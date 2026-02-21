using System.Net.WebSockets;
using System.Collections.Concurrent;

namespace SimpleMessenger.Services;

public interface IClientManager
{
    void AddClient(string id, WebSocket socket);
    void RemoveClient(string id);
    IReadOnlyDictionary<string, WebSocket> GetAllClients();
    WebSocket? GetClient(string id);
}
