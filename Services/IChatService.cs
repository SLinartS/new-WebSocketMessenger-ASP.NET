/*
 * IChatService.cs - Интерфейс сервиса чата
 * 
 * Аналог в PHP: интерфейс (interface) - контракт, который должен реализовать класс
 * В .NET интерфейсы похожи на PHP 8+ интерфейсы
 * 
 * Используется для Dependency Injection - вместо конкретного класса подставляется реализация
 * Аналог в PHP: type-hinting на интерфейс $chatService->method(IChatService $service)
 */

using System.Net.WebSockets;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

/*
 * Интерфейс определяет контракт (методы), которые должны быть реализованы
 * Аналог в PHP:
 * interface IChatService 
 * {
 *     public function sendMessage(string $clientId, ChatMessage $message): Task;
 *     public function broadcast(ChatMessage $message, ?string $excludeClientId = null): Task;
 *     public function handleClient(string $clientId, WebSocket $socket, CancellationToken $ct): Task;
 * }
 */
public interface IChatService
{
    /*
     * Отправить сообщение конкретному клиенту
     * 
     * Аналог в PHP:
     * public function sendMessage(string $clientId, ChatMessage $message): void;
     */
    Task SendMessageAsync(string clientId, ChatMessage message);

    /*
     * Рассылка сообщения всем клиентам
     * 
     * @param message - сообщение для рассылки
     * @param excludeClientId - ID клиента, которого нужно исключить (отправитель)
     * 
     * Аналог в PHP:
     * public function broadcast(ChatMessage $message, ?string $excludeClientId = null): void;
     */
    Task BroadcastAsync(ChatMessage message, string? excludeClientId = null);

    /*
     * Обработать соединение клиента
     * 
     * @param clientId - уникальный ID клиента
     * @param socket - WebSocket соединение
     * @param ct - токен отмены (для graceful shutdown)
     * 
     * Аналог в PHP:
     * public function handleClient(string $clientId, $socket, CancellationToken $ct): void;
     */
    Task HandleClientAsync(string clientId, WebSocket socket, CancellationToken ct);
}
