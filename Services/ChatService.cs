/*
 * ChatService.cs - Основной сервис для работы с чатом
 * 
 * Аналог в PHP: это Controller или Service класс (например, ChatController)
 * Обрабатывает WebSocket соединения и рассылку сообщений
 */

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleMessenger.Models;
using Microsoft.Extensions.Logging;

namespace SimpleMessenger.Services;

/*
 * Реализация интерфейса IChatService
 * 
 * В .NET используется DI (Dependency Injection), поэтому конструктор автоматически
 * получает зависимости (IClientManager, ILogger)
 * 
 * Аналог в PHP с Laravel: class ChatService implements IChatService
 * но без автоматического внедрения зависимостей (нужно использовать $this->app->make())
 */
public class ChatService : IChatService
{
    // Менеджер клиентов - хранит все активные WebSocket соединения
    // Аналог: $this->clientManager = new ClientManager();
    private readonly IClientManager _clientManager;
    
    // Логгер для записи событий (аналог: Logger::info() в Laravel или error_log() в PHP)
    private readonly ILogger<ChatService> _logger;

    /*
     * Конструктор с внедрением зависимостей
     * .NET автоматически передаст экземпляры IClientManager и ILogger<ChatService>
     * 
     * Аналог в PHP (Laravel):
     * public function __construct(ClientManager $clientManager, Logger $logger)
     * {
     *     $this->clientManager = $clientManager;
     *     $this->logger = $logger;
     * }
     */
    public ChatService(IClientManager clientManager, ILogger<ChatService> logger)
    {
        _clientManager = clientManager;
        _logger = logger;
    }

    /*
     * Отправить сообщение конкретному клиенту
     * 
     * Аналог в PHP:
     * public function sendMessage(string $clientId, array $message): void
     */
    public async Task SendMessageAsync(string clientId, ChatMessage message)
    {
        var client = _clientManager.GetClient(clientId);
        if (client?.State != WebSocketState.Open) return;

        // Сериализация объекта в JSON
        // Аналог: json_encode($message) в PHP
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Отправка данных через WebSocket
        // Аналог: $client->send($data) в Ratchet (PHP WebSocket library)
        await client.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,  // Тип сообщения - текст
            true,  // EndOfMessage - true, если это последняя часть сообщения
            CancellationToken.None);
    }

    /*
     * Рассылка сообщения всем подключённым клиентам
     * 
     * Аналог в PHP (Ratchet):
     * foreach ($clients as $client) {
     *     $client->send(json_encode($message));
     * }
     * 
     * @param message - сообщение для рассылки
     * @param excludeClientId - ID клиента, которому НЕ нужно отправлять (отправитель)
     */
    public async Task BroadcastAsync(ChatMessage message, string? excludeClientId = null)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var sentCount = 0;

        // Получаем всех клиентов и рассылаем сообщение
        foreach (var (id, client) in _clientManager.GetAllClients())
        {
            // Пропускаем отправителя и неактивные соединения
            if (id == excludeClientId || client.State != WebSocketState.Open)
                continue;

            try
            {
                await client.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                sentCount++;
            }
            catch (Exception ex)
            {
                // Логирование ошибки и удаление "мёртвого" клиента
                _logger.LogError(ex, "Error sending to client {ClientId}", id);
                _clientManager.RemoveClient(id);
            }
        }

        _logger.LogInformation("Broadcast to {Count} clients", sentCount);
    }

    /*
     * Основной метод обработки WebSocket соединения клиента
     * Вызывается при подключении нового клиента
     * 
     * Аналог в PHP (Ratchet):
     * public function onMessage(Message $msg, ConnectionInterface $conn)
     * {
     *     // обработка входящего сообщения
     * }
     * 
     * @param clientId - уникальный ID клиента (генерируется при подключении)
     * @param socket - WebSocket соединение клиента
     * @param ct - CancellationToken дляGraceful завершения (аналог: сигнал SIGTERM)
     */
    public async Task HandleClientAsync(string clientId, WebSocket socket, CancellationToken ct)
    {
        // Регистрируем нового клиента
        _clientManager.AddClient(clientId, socket);
        
        // Оповещаем всех о новом пользователе
        await BroadcastAsync(ChatMessage.System($"User {clientId} connected"));

        _logger.LogInformation("Client connected: {ClientId}", clientId);

        // Буфер для приёма данных (аналог: $buffer = '' в PHP)
        var buffer = new byte[4096];

        try
        {
            // Основной цикл обработки сообщений
            // Аналог: while ($connection->isConnected()) { ... } в PHP
            while (socket.State == WebSocketState.Open)
            {
                // Получение данных от клиента
                // Блокирует выполнение до получения данных (аналог: fgets() в PHP)
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                // Клиент отправил команду закрытия соединения
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                // Обработка текстового сообщения
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Декодирование байтов в строку
                    // Аналог: $json = $buffer в PHP (уже строка)
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // Десериализация JSON в объект ChatMessage
                    // Аналог: $message = json_decode($json, true) в PHP
                    var message = JsonSerializer.Deserialize<ChatMessage>(json);

                    if (!string.IsNullOrWhiteSpace(message?.Text))
                    {
                        // Рассылаем сообщение всем, кроме отправителя
                        // Аналог: $this->broadcast($message, $excludeClientId);
                        await BroadcastAsync(
                            ChatMessage.Create(message.Text, message.Name ?? "Anonymous"), 
                            clientId);
                    }
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            // Клиент отключился неожиданно (аналог: соединение разорвалось)
            _logger.LogWarning("Client {ClientId} disconnected unexpectedly", clientId);
        }
        catch (OperationCanceledException)
        {
            // Соединение было отменено (аналог: таймаут или принудительное закрытие)
            _logger.LogInformation("Connection with {ClientId} cancelled", clientId);
        }
        catch (Exception ex)
        {
            // Любая другая ошибка
            _logger.LogError(ex, "Error processing client {ClientId}", clientId);
        }
        finally
        {
            // Очистка: удаляем клиента и оповещаем всех
            _clientManager.RemoveClient(clientId);
            await BroadcastAsync(ChatMessage.System($"{clientId} disconnected"));

            // Корректное закрытие WebSocket соединения
            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);

            _logger.LogInformation("Client disconnected: {ClientId}", clientId);
        }
    }
}
