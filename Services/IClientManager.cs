/*
 * IClientManager.cs - Интерфейс менеджера клиентов
 * 
 * Аналог в PHP: интерфейс для управления подключёнными клиентами
 * Определяет контракт для хранения и управления WebSocket соединениями
 */

using System.Net.WebSockets;
using System.Collections.Concurrent;

namespace SimpleMessenger.Services;

/*
 * Интерфейс менеджера клиентов
 * 
 * Аналог в PHP:
 * interface IClientManager 
 * {
 *     public function addClient(string $id, WebSocket $socket): void;
 *     public function removeClient(string $id): void;
 *     public function getAllClients(): array;
 *     public function getClient(string $id): ?WebSocket;
 * }
 */
public interface IClientManager
{
    /*
     * Добавить клиента
     * 
     * Аналог в PHP:
     * public function addClient(string $id, $socket): void;
     */
    void AddClient(string id, WebSocket socket);

    /*
     * Удалить клиента
     * 
     * Аналог в PHP:
     * public function removeClient(string $id): void;
     */
    void RemoveClient(string id);

    /*
     * Получить всех клиентов
     * 
     * Аналог в PHP:
     * public function getAllClients(): array;
     */
    IReadOnlyDictionary<string, WebSocket> GetAllClients();

    /*
     * Получить конкретного клиента
     * 
     * Аналог в PHP:
     * public function getClient(string $id): ?WebSocket;
     */
    WebSocket? GetClient(string id);
}
