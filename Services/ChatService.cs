using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Linq;
using SimpleMessenger.Models;
using Microsoft.Extensions.Logging;

namespace SimpleMessenger.Services;

public class ChatService : IChatService
{
    private readonly IClientManager _clientManager;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IClientManager clientManager, IMessageRepository messageRepository, ILogger<ChatService> logger)
    {
        _clientManager = clientManager;
        _messageRepository = messageRepository;
        _logger = logger;
        
        // Subscribe to user changes to broadcast updates
        _clientManager.UsersChanged += async _ => await BroadcastUsersListAsync();
    }

    public async Task<List<ChatMessage>> GetMessageHistoryAsync()
    {
        return await _messageRepository.GetAllAsync();
    }

    public async Task ClearChatAsync()
    {
        await _messageRepository.ClearAsync();
        await BroadcastAsync(ChatMessage.System("Chat has been cleared"));
    }

    public async Task SendMessageAsync(string clientId, ChatMessage message)
    {
        var client = _clientManager.GetClient(clientId);
        if (client?.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        await client.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    public async Task BroadcastAsync(ChatMessage message, string? excludeClientId = null)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var sentCount = 0;

        foreach (var (id, client) in _clientManager.GetAllClients())
        {
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
                _logger.LogError(ex, "Error sending to client {ClientId}", id);
                _clientManager.RemoveClient(id);
            }
        }

        _logger.LogInformation("Broadcast to {Count} clients", sentCount);
    }

    public async Task BroadcastUsersListAsync()
    {
        var users = _clientManager.GetActiveUsers().Select(u => new
        {
            id = u.Id,
            nickname = u.Nickname,
            ipAddress = u.IpAddress
        }).ToList();
        var message = new { type = "usersList", users };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(message, options);
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var (id, client) in _clientManager.GetAllClients())
        {
            if (client.State != WebSocketState.Open)
                continue;

            try
            {
                await client.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending users list to client {ClientId}", id);
            }
        }
    }

    public async Task HandleClientAsync(string clientId, WebSocket socket, string ipAddress, CancellationToken ct)
    {
        _clientManager.AddClient(clientId, socket, ipAddress);

        var history = await _messageRepository.GetAllAsync();
        foreach (var msg in history)
        {
            await SendMessageAsync(clientId, msg);
        }

        await BroadcastUsersListAsync();
        await BroadcastAsync(ChatMessage.System($"User {clientId} connected"));

        _logger.LogInformation("Client connected: {ClientId} from {IpAddress}", clientId, ipAddress);

        var buffer = new byte[4096];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonSerializer.Deserialize<ChatMessage>(json);

                    if (!string.IsNullOrWhiteSpace(message?.Text))
                    {
                        if (message.Type == "clear")
                        {
                            await ClearChatAsync();
                        }
                        else
                        {
                            var nickname = message.Name ?? "Anonymous";
                            _clientManager.UpdateUserNickname(clientId, nickname);
                            
                            var chatMessage = ChatMessage.Create(message.Text, nickname);
                            await _messageRepository.AddAsync(chatMessage);
                            await BroadcastAsync(chatMessage, clientId);
                        }
                    }
                    else if (message?.Type == "nickname")
                    {
                        // Update nickname without sending a message
                        var nickname = message.Name ?? "Anonymous";
                        _clientManager.UpdateUserNickname(clientId, nickname);
                    }
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogWarning("Client {ClientId} disconnected unexpectedly", clientId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection with {ClientId} cancelled", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing client {ClientId}", clientId);
        }
        finally
        {
            _clientManager.RemoveClient(clientId);
            await BroadcastUsersListAsync();
            await BroadcastAsync(ChatMessage.System($"{clientId} disconnected"));

            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);

            _logger.LogInformation("Client disconnected: {ClientId}", clientId);
        }
    }
}
