namespace SimpleMessenger.Services;

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleMessenger.Models;

public class ChatService : IChatService
{
    private const string DefaultChatRoomId = "general";
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IClientManager _clientManager;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IClientManager clientManager, IMessageRepository messageRepository, ILogger<ChatService> logger)
    {
        this._clientManager = clientManager;
        this._messageRepository = messageRepository;
        this._logger = logger;

        this._clientManager.UsersChanged += _ =>
            this.BroadcastUsersListAsync()
                .ContinueWith(
                    t => this._logger.LogError(t.Exception, "Error broadcasting users list after user change"),
                    TaskContinuationOptions.OnlyOnFaulted
                );
    }

    private static string SerializeMessage(object message) =>
        JsonSerializer.Serialize(message, _jsonOptions);

    private void HandleError(string clientId, Exception ex, string operation) =>
        this._logger.LogError(ex, "Error during {Operation} for client {ClientId}", operation, clientId);

    private void LogRequest(string clientId, string operation, string details = "") =>
        this._logger.LogInformation(
            "Request - Client: {ClientId}, Operation: {Operation}, Details: {Details}",
            clientId, operation, details);

    private static Task SendBytesAsync(WebSocket client, byte[] messageBytes, CancellationToken ct) =>
        client.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct);

    private async Task BroadcastToClientsAsync(
        IEnumerable<(string Id, WebSocket Client)> clients,
        byte[] messageBytes,
        string? excludeClientId = null,
        CancellationToken ct = default,
        string? logTemplate = null)
    {
        var sentCount = 0;

        foreach (var (id, client) in clients)
        {
            if (id == excludeClientId || client.State != WebSocketState.Open)
            {
                continue;
            }

            try
            {
                await SendBytesAsync(client, messageBytes, ct);
                sentCount++;
            }
            catch (Exception ex)
            {
                this.HandleError(id, ex, nameof(BroadcastToClientsAsync));
                this._clientManager.RemoveClient(id);
            }
        }

        if (logTemplate != null)
        {
            this._logger.LogInformation(logTemplate, sentCount);
        }
    }

    public async Task<List<ChatMessage>> GetMessageHistoryAsync() =>
        await this._messageRepository.GetAllAsync();

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId) =>
        await this._messageRepository.GetMessagesByChatAsync(chatRoomId);

    public async Task<List<OnlineUser>> GetOnlineUsersByChatAsync(string chatRoomId)
    {
        var participants = await this._messageRepository.GetParticipantsByChatAsync(chatRoomId);
        var participantIds = new HashSet<string>(participants.Select(p => p.UserId));
        return [.. this._clientManager.GetActiveUsers()
            .Where(u => participantIds.Contains(u.Id))
            .Select(MapToOnlineUser)];
    }


    public async Task SendMessageAsync(string clientId, ChatMessage message)
    {
        var client = this._clientManager.GetClient(clientId);
        if (client?.State != WebSocketState.Open)
        {
            return;
        }

        this.LogRequest(clientId, "SendMessage", $"Message type: {message.Type}");

        var bytes = Encode(message);
        await SendBytesAsync(client, bytes, CancellationToken.None);
    }

    public async Task BroadcastAsync(ChatMessage message, string? excludeClientId = null)
    {
        var bytes = Encode(message);
        var clients = this._clientManager.GetAllClients()
            .Select(kv => (kv.Key, kv.Value));

        await this.BroadcastToClientsAsync(
            clients,
            bytes,
            excludeClientId,
            logTemplate: "Broadcast to {Count} clients");
    }

    public async Task BroadcastToChatAsync(string chatRoomId, ChatMessage message, string? excludeClientId = null)
    {
        var participants = await this._messageRepository.GetParticipantsByChatAsync(chatRoomId);
        var bytes = Encode(message);

        var clients = participants
            .Select(p => (p.UserId, this._clientManager.GetClient(p.UserId)))
            .Where(pair => pair.Item2 != null)
            .Select(pair => (pair.UserId, pair.Item2!));

        await this.BroadcastToClientsAsync(
            clients,
            bytes,
            excludeClientId,
            logTemplate: null);

        this._logger.LogInformation("Broadcast to chat {ChatId}", chatRoomId);
    }

    public async Task BroadcastUsersListAsync()
    {
        var users = this._clientManager.GetActiveUsers()
            .Select(u => new
            {
                id = u.Id,
                nickname = u.Nickname,
                // ip intentionally omitted (Finding 1 – security)
                isTyping = u.IsTyping
            }).ToList();

        var bytes = Encode(new { type = "usersList", users });
        var clients = this._clientManager.GetAllClients()
            .Select(kv => (kv.Key, kv.Value));

        await this.BroadcastToClientsAsync(clients, bytes);
    }

    public async Task BroadcastChatUpdateAsync(string chatRoomId, List<ChatMessage> messages, List<OnlineUser> onlineUsers)
    {
        var users = onlineUsers.Select(u => new
        {
            id = u.Id,
            nickname = u.Nickname,
            // ip intentionally omitted (Finding 1 – security)
            isTyping = u.IsTyping
        }).ToList();

        var bytes = Encode(new { type = "chatUpdate", chatRoomId, messages, users });
        var participants = await this._messageRepository.GetParticipantsByChatAsync(chatRoomId);

        var clients = participants
            .Select(p => (p.UserId, this._clientManager.GetClient(p.UserId)))
            .Where(pair => pair.Item2 != null)
            .Select(pair => (pair.UserId, pair.Item2!));

        await this.BroadcastToClientsAsync(clients, bytes);
    }

    public async Task BroadcastTypingStatusAsync(string userId, string nickname, bool isTyping)
    {
        var bytes = Encode(new { type = "typing", userId, nickname, isTyping });
        var clients = this._clientManager.GetAllClients()
            .Select(kv => (kv.Key, kv.Value));

        await this.BroadcastToClientsAsync(clients, bytes, excludeClientId: userId);
    }

    // -------------------------------------------------------------------------
    // IChatService – clear
    // -------------------------------------------------------------------------

    public async Task ClearChatAsync()
    {
        await this._messageRepository.ClearAsync();
        await this.BroadcastAsync(ChatMessage.System("Chat has been cleared"));
    }

    public async Task ClearChatAsync(string chatRoomId)
    {
        await this._messageRepository.ClearMessagesByChatAsync(chatRoomId);
        await this.BroadcastToChatAsync(chatRoomId, ChatMessage.Clear($"Chat {chatRoomId} has been cleared", chatRoomId));
    }

    // -------------------------------------------------------------------------
    // IChatService – WebSocket connection lifecycle
    // -------------------------------------------------------------------------

    public async Task HandleClientAsync(string clientId, WebSocket socket, string ipAddress, CancellationToken ct)
    {
        this._clientManager.AddClient(clientId, socket, ipAddress);

        // Send existing history to the newly-connected client
        var history = await this._messageRepository.GetAllAsync();
        foreach (var msg in history)
        {
            await this.SendMessageAsync(clientId, msg);
        }

        await this.BroadcastUsersListAsync();

        this._logger.LogInformation("Client connected: {ClientId} from {IpAddress}", clientId, ipAddress);

        // Step 10: accumulate multi-frame WebSocket messages into a MemoryStream
        using var accum = new MemoryStream();

        try
        {
            var buffer = new byte[4096];

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                var clientClosedNormally = false;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        clientClosedNormally = true;
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        await accum.WriteAsync(buffer.AsMemory(0, result.Count), ct);
                    }
                }
                while (!result.EndOfMessage);

                if (clientClosedNormally)
                {
                    break;
                }

                if (accum.Length == 0)
                {
                    continue;
                }

                var json = Encoding.UTF8.GetString(accum.ToArray());
                accum.SetLength(0); // reset for next message

                ChatMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<ChatMessage>(json, _jsonOptions);
                }
                catch (JsonException ex)
                {
                    this._logger.LogWarning(ex, "Client {ClientId} sent malformed JSON", clientId);
                    continue;
                }

                if (message is null)
                {
                    continue;
                }

                // Step 13: dispatch via switch expression instead of nested if/else
                await this.DispatchMessageAsync(clientId, message);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            this._logger.LogWarning("Client {ClientId} disconnected unexpectedly", clientId);
        }
        catch (OperationCanceledException)
        {
            this._logger.LogInformation("Connection with {ClientId} cancelled", clientId);
        }
        catch (Exception ex)
        {
            // Step 5: single log – HandleError already calls _logger.LogError
            this.HandleError(clientId, ex, nameof(HandleClientAsync));
        }
        finally
        {
            this._clientManager.RemoveClient(clientId);
            await this.BroadcastUsersListAsync();

            if (socket.State == WebSocketState.Open)
            {
                // Step 9: use a fresh token so CloseAsync is never called with
                // an already-cancelled token during server shutdown.
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", closeCts.Token);
                }
                catch (Exception ex)
                {
                    this._logger.LogWarning(ex, "Error closing WebSocket for {ClientId}", clientId);
                }
            }

            this._logger.LogInformation("Client disconnected: {ClientId}", clientId);
        }
    }

    // -------------------------------------------------------------------------
    // Step 13: private per-type message handlers
    // -------------------------------------------------------------------------

    private async Task DispatchMessageAsync(string clientId, ChatMessage message)
    {
        // Types that carry chat content also require non-empty Text
        if (message.Type is "message" or "findUser")
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                return;
            }
        }

        await (message.Type switch
        {
            "findUser" => this.HandleFindUserAsync(clientId, message),
            "clear" => this.HandleClearAsync(message),
            "switchChat" => this.HandleSwitchChatAsync(clientId, message),
            "nickname" => this.HandleNicknameAsync(clientId, message),
            "typing" => this.HandleTypingAsync(clientId, message),
            _ => this.HandleChatMessageAsync(clientId, message)
        });
    }

    private async Task HandleFindUserAsync(string clientId, ChatMessage message) =>
        // Echo back to the requesting client so the client JS can fire the API call
        await this.SendMessageAsync(clientId, message);

    private async Task HandleClearAsync(ChatMessage message)
    {
        var chatRoomId = message.ChatRoomId ?? DefaultChatRoomId;
        await this.ClearChatAsync(chatRoomId);
    }

    private async Task HandleSwitchChatAsync(string clientId, ChatMessage message)
    {
        var chatRoomId = message.ChatRoomId ?? DefaultChatRoomId;
        this._clientManager.UpdateUserCurrentChat(clientId, chatRoomId);

        var messages = await this.GetMessagesByChatAsync(chatRoomId);
        var onlineUsers = await this.GetOnlineUsersByChatAsync(chatRoomId);
        await this.BroadcastChatUpdateAsync(chatRoomId, messages, onlineUsers);
    }

    private Task HandleNicknameAsync(string clientId, ChatMessage message)
    {
        var nickname = SanitizeNickname(message.Name);
        this._clientManager.UpdateUserNickname(clientId, nickname);
        return Task.CompletedTask;
    }

    private async Task HandleTypingAsync(string clientId, ChatMessage message)
    {
        var nickname = SanitizeNickname(message.Name);
        this._clientManager.UpdateUserTypingStatus(clientId, message.IsTyping);
        await this.BroadcastTypingStatusAsync(clientId, nickname, message.IsTyping);
    }

    private async Task HandleChatMessageAsync(string clientId, ChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var nickname = SanitizeNickname(message.Name);
        var chatRoomId = message.ChatRoomId ?? DefaultChatRoomId;

        this._clientManager.UpdateUserNickname(clientId, nickname);

        // Step 7 (SanitizeInput removed): store raw text; client escapes on render
        var chatMessage = ChatMessage.Create(message.Text, nickname, chatRoomId, clientId);
        await this._messageRepository.AddAsync(chatMessage);
        await this.BroadcastToChatAsync(chatRoomId, chatMessage, clientId);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sanitizes a display name to a printable, non-empty string.
    /// HTML encoding is deliberately NOT done here; the client handles rendering.
    /// </summary>
    private static string SanitizeNickname(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Anonymous" : name.Trim();

    private static byte[] Encode(object payload) =>
        Encoding.UTF8.GetBytes(SerializeMessage(payload));

    private static OnlineUser MapToOnlineUser(ActiveUser u) => new()
    {
        Id = u.Id,
        Nickname = u.Nickname,
        IpAddress = string.Empty, // ip not exposed to clients (Finding 1 – security)
        IsTyping = u.IsTyping
    };
}
