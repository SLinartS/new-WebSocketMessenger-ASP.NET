using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class ChatService : IChatService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId)
    {
        return await _messageRepository.GetMessagesByChatAsync(chatRoomId);
    }

    public async Task<List<OnlineUser>> GetOnlineUsersByChatAsync(string chatRoomId)
    {
        var participants = await _messageRepository.GetParticipantsByChatAsync(chatRoomId);
        var activeUsers = _clientManager.GetActiveUsers().ToList();
        return activeUsers.Where(u => participants.Any(p => p.UserId == u.Id))
            .Select(u => new OnlineUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IpAddress = u.IpAddress,
                IsTyping = u.IsTyping
            }).ToList();
    }

    public async Task ClearChatAsync()
    {
        await _messageRepository.ClearAsync();
        await BroadcastAsync(ChatMessage.System("Chat has been cleared"));
    }

    public async Task ClearChatAsync(string chatRoomId)
    {
        await _messageRepository.ClearMessagesByChatAsync(chatRoomId);
        await BroadcastToChatAsync(chatRoomId, ChatMessage.Clear($"Chat {chatRoomId} has been cleared", chatRoomId));
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

    public async Task BroadcastToChatAsync(string chatRoomId, ChatMessage message, string? excludeClientId = null)
    {
        var participants = await _messageRepository.GetParticipantsByChatAsync(chatRoomId);
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var sentCount = 0;

        foreach (var participant in participants)
        {
            if (participant.UserId == excludeClientId) continue;

            var client = _clientManager.GetClient(participant.UserId);
            if (client?.State != WebSocketState.Open) continue;

            try
            {
                await client.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending to client {ClientId}", participant.UserId);
                _clientManager.RemoveClient(participant.UserId);
            }
        }

        _logger.LogInformation("Broadcast to chat {ChatId} ({Count} clients)", chatRoomId, sentCount);
    }

    public async Task BroadcastUsersListAsync()
    {
        var users = _clientManager.GetActiveUsers().Select(u => new
        {
            id = u.Id,
            nickname = u.Nickname,
            ipAddress = u.IpAddress,
            isTyping = u.IsTyping
        }).ToList();
        var message = new { type = "usersList", users };
        var json = JsonSerializer.Serialize(message, _jsonOptions);
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

    public async Task BroadcastChatUpdateAsync(string chatRoomId, List<ChatMessage> messages, List<OnlineUser> onlineUsers)
    {
        var users = onlineUsers.Select(u => new
        {
            id = u.Id,
            nickname = u.Nickname,
            ipAddress = u.IpAddress,
            isTyping = u.IsTyping
        }).ToList();

        var chatMessage = new { type = "chatUpdate", chatRoomId, messages, users };
        var json = JsonSerializer.Serialize(chatMessage, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        var participants = await _messageRepository.GetParticipantsByChatAsync(chatRoomId);
        foreach (var participant in participants)
        {
            var client = _clientManager.GetClient(participant.UserId);
            if (client?.State != WebSocketState.Open) continue;

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
                _logger.LogError(ex, "Error sending chat update to client {ClientId}", participant.UserId);
            }
        }
    }

    public async Task BroadcastTypingStatusAsync(string userId, string nickname, bool isTyping)
    {
        var message = new { type = "typing", userId, nickname, isTyping };
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var (id, client) in _clientManager.GetAllClients())
        {
            if (id == userId || client.State != WebSocketState.Open)
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
                _logger.LogError(ex, "Error sending typing status to client {ClientId}", id);
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
                        if (message.Type == "findUser")
                        {
                            // Handle user search - send back to client for API call
                            await SendMessageAsync(clientId, message);
                        }
                        else
                        {
                            var nickname = message.Name ?? "Anonymous";
                            _clientManager.UpdateUserNickname(clientId, nickname);

                            var chatRoomId = message.ChatRoomId ?? "general";
                            var chatMessage = ChatMessage.Create(message.Text, nickname, chatRoomId, clientId);
                            await _messageRepository.AddAsync(chatMessage);
                            await BroadcastToChatAsync(chatRoomId, chatMessage, clientId);
                        }
                    }

                    if (message?.Type == "clear")
                    {
                        var chatRoomId = message.ChatRoomId ?? "general";
                        await ClearChatAsync(chatRoomId);
                    }
                    else if (message?.Type == "switchChat")
                    {
                        // Handle chat switch
                        var chatRoomId = message.ChatRoomId ?? "general";
                        _clientManager.UpdateUserCurrentChat(clientId, chatRoomId);

                        var messages = await GetMessagesByChatAsync(chatRoomId);
                        var onlineUsers = await GetOnlineUsersByChatAsync(chatRoomId);
                        await BroadcastChatUpdateAsync(chatRoomId, messages, onlineUsers);
                    }
                    else if (message?.Type == "nickname")
                    {
                        // Update nickname without sending a message
                        var nickname = message.Name ?? "Anonymous";
                        _clientManager.UpdateUserNickname(clientId, nickname);
                    }
                    else if (message?.Type == "typing")
                    {
                        // Update typing status
                        var nickname = message.Name ?? "Anonymous";
                        _clientManager.UpdateUserTypingStatus(clientId, message.IsTyping);
                        await BroadcastTypingStatusAsync(clientId, nickname, message.IsTyping);
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

            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);

            _logger.LogInformation("Client disconnected: {ClientId}", clientId);
        }
    }
}
