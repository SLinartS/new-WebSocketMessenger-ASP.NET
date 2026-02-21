/*
 * ClientManager.cs - Менеджер подключённых клиентов
 * 
 * Аналог в PHP: класс для хранения активных соединений
 * (в Ratchet это часть Component/ConnectionContainerInterface)
 * 
 * Использует ConcurrentDictionary - потокобезопасный ассоциативный массив
 * Аналог в PHP: SplObjectStorage или array с блокировками
 */

using System.Net.WebSockets;
using System.Collections.Concurrent;

namespace SimpleMessenger.Services;

/*
 * Реализация интерфейса IClientManager
 * 
 * Паттерн Singleton - создаётся один экземпляр при старте приложения
 * Хранится в памяти всё время работы сервера
 */
public class ClientManager : IClientManager
{
    // ConcurrentDictionary - потокобезопасный словарь (аналог: SplObjectStorage в PHP)
    // Ключ = clientId (строка), Значение = WebSocket соединение
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();

    /*
     * Добавить нового клиента
     * 
     * Аналог в PHP (Ratchet):
     * $this->clients[$clientId] = $connection;
     */
    public void AddClient(string id, WebSocket socket) => 
        _clients[id] = socket;

    /*
     * Удалить клиента при отключении
     * 
     * Аналог в PHP:
     * unset($this->clients[$clientId]);
     */
    public void RemoveClient(string id) => 
        _clients.TryRemove(id, out _);

    /*
     * Получить всех клиентов
     * 
     * Аналог в PHP:
     * return $this->clients; // или итератор
     */
    public IReadOnlyDictionary<string, WebSocket> GetAllClients() => _clients;
    
    /*
     * Получить конкретного клиента по ID
     * 
     * Аналог в PHP:
     * return $this->clients[$clientId] ?? null;
     */
    public WebSocket? GetClient(string id) => 
        _clients.TryGetValue(id, out var client) ? client : null;
}
