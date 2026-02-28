
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class ChatService : IChatService
{
    private const string DefaultChatRoomId = "general";
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IClientManager clientManager;
    private readonly IMessageRepository messageRepository;
    private readonly ILogger<ChatService> logger;

    public ChatService(
        IClientManager clientManager,
        IMessageRepository messageRepository,
        ILogger<ChatService> logger
    )
    {
        this.clientManager = clientManager;
        this.messageRepository = messageRepository;
        this.logger = logger;

        this.clientManager.UsersChanged += _ =>
            BroadcastUsersListAsync()
                .ContinueWith(
                    t =>
                        this.logger.LogError(
                            t.Exception,
                            "Error broadcasting users list after user change"
                        ),
                    TaskContinuationOptions.OnlyOnFaulted
                );
    }

    private static string SerializeMessage(object message) =>
        JsonSerializer.Serialize(message, jsonOptions);

    private void HandleError(string clientId, Exception ex, string operation) =>
        logger.LogError(
            ex,
            "Error during {Operation} for client {ClientId}",
            operation,
            clientId
        );

    private void LogRequest(string clientId, string operation, string details = "") =>
        logger.LogInformation(
            "Request - Client: {ClientId}, Operation: {Operation}, Details: {Details}",
            clientId,
            operation,
            details
        );

    private static Task SendBytesAsync(
        WebSocket client,
        byte[] messageBytes,
        CancellationToken ct
    ) =>
        client.SendAsync(
            new ArraySegment<byte>(messageBytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct
        );

    private async Task BroadcastToClientsAsync(
        IEnumerable<(string Id, WebSocket Client)> clients,
        byte[] messageBytes,
        string? excludeClientId = null,
        string? logTemplate = null,
        CancellationToken ct = default
    )
    {
        int sentCount = 0;

        foreach ((string? id, WebSocket? client) in clients)
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
                HandleError(id, ex, nameof(BroadcastToClientsAsync));
                clientManager.RemoveClient(id);
            }
        }

        if (logTemplate != null)
        {
            logger.LogInformation(logTemplate, sentCount);
        }
    }

    public async Task<List<ChatMessage>> GetMessageHistoryAsync() =>
        await messageRepository.GetAllAsync();

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId) =>
        await messageRepository.GetMessagesByChatAsync(chatRoomId);

    public async Task<List<OnlineUser>> GetOnlineUsersByChatAsync(string chatRoomId)
    {
        List<ChatParticipant> participants = await messageRepository.GetParticipantsByChatAsync(chatRoomId);
        var participantIds = new HashSet<string>(participants.Select(p => p.UserId));
        return
        [
            ..
                clientManager.GetActiveUsers()
                .Where(u => participantIds.Contains(u.Id))
                .Select(MapToOnlineUser),
        ];
    }

    public async Task SendMessageAsync(string clientId, ChatMessage message)
    {
        WebSocket? client = clientManager.GetClient(clientId);
        if (client?.State != WebSocketState.Open)
        {
            return;
        }

        LogRequest(clientId, "SendMessage", $"Message type: {message.Type}");

        byte[] bytes = Encode(message);
        await SendBytesAsync(client, bytes, CancellationToken.None);
    }

    public async Task BroadcastAsync(ChatMessage message, string? excludeClientId = null)
    {
        byte[] bytes = Encode(message);
        IEnumerable<(string Key, WebSocket Value)> clients = clientManager.GetAllClients().Select(kv => (kv.Key, kv.Value));

        await BroadcastToClientsAsync(
            clients,
            bytes,
            excludeClientId,
            logTemplate: "Broadcast to {Count} clients"
        );
    }

    public async Task BroadcastToChatAsync(
        string chatRoomId,
        ChatMessage message,
        string? excludeClientId = null
    )
    {
        List<ChatParticipant> participants = await messageRepository.GetParticipantsByChatAsync(chatRoomId);
        byte[] bytes = Encode(message);

        IEnumerable<(string UserId, WebSocket)> clients = participants
            .Select(p => (p.UserId, clientManager.GetClient(p.UserId)))
            .Where(pair => pair.Item2 != null)
            .Select(pair => (pair.UserId, pair.Item2!));

        await BroadcastToClientsAsync(clients, bytes, excludeClientId, logTemplate: null);

        logger.LogInformation("Broadcast to chat {ChatId}", chatRoomId);
    }

    public async Task BroadcastUsersListAsync()
    {
        var users =
            clientManager.GetActiveUsers()
            .Select(u => new
            {
                id = u.Id,
                nickname = u.Nickname,
                // ip intentionally omitted (Finding 1 – security)
                isTyping = u.IsTyping,
            })
            .ToList();

        byte[] bytes = Encode(new { type = "usersList", users });
        IEnumerable<(string Key, WebSocket Value)> clients = clientManager.GetAllClients().Select(kv => (kv.Key, kv.Value));

        await BroadcastToClientsAsync(clients, bytes);
    }

    public async Task BroadcastChatUpdateAsync(
        string chatRoomId,
        List<ChatMessage> messages,
        List<OnlineUser> onlineUsers
    )
    {
        var users = onlineUsers
            .Select(u => new
            {
                id = u.Id,
                nickname = u.Nickname,
                // ip intentionally omitted (Finding 1 – security)
                isTyping = u.IsTyping,
            })
            .ToList();

        byte[] bytes = Encode(
            new
            {
                type = "chatUpdate",
                chatRoomId,
                messages,
                users,
            }
        );
        List<ChatParticipant> participants = await messageRepository.GetParticipantsByChatAsync(chatRoomId);

        IEnumerable<(string UserId, WebSocket)> clients = participants
            .Select(p => (p.UserId, clientManager.GetClient(p.UserId)))
            .Where(pair => pair.Item2 != null)
            .Select(pair => (pair.UserId, pair.Item2!));

        await BroadcastToClientsAsync(clients, bytes);
    }

    public async Task BroadcastTypingStatusAsync(string userId, string nickname, bool isTyping)
    {
        byte[] bytes = Encode(
            new
            {
                type = "typing",
                userId,
                nickname,
                isTyping,
            }
        );
        IEnumerable<(string Key, WebSocket Value)> clients = clientManager.GetAllClients().Select(kv => (kv.Key, kv.Value));

        await BroadcastToClientsAsync(clients, bytes, excludeClientId: userId);
    }

    // -------------------------------------------------------------------------
    // IChatService – clear
    // -------------------------------------------------------------------------

    public async Task ClearChatAsync()
    {
        await messageRepository.ClearAsync();
        await BroadcastAsync(ChatMessage.System("Chat has been cleared"));
    }

    public async Task ClearChatAsync(string chatRoomId)
    {
        await messageRepository.ClearMessagesByChatAsync(chatRoomId);
        await BroadcastToChatAsync(
            chatRoomId,
            ChatMessage.Clear($"Chat {chatRoomId} has been cleared", chatRoomId)
        );
    }

    // -------------------------------------------------------------------------
    // IChatService – WebSocket connection lifecycle
    // -------------------------------------------------------------------------

    public async Task HandleClientAsync(
        string clientId,
        WebSocket socket,
        string ipAddress,
        CancellationToken ct
    )
    {
        clientManager.AddClient(clientId, socket, ipAddress);

        // Send existing history to the newly-connected client
        List<ChatMessage> history = await messageRepository.GetAllAsync();
        foreach (ChatMessage msg in history)
        {
            await SendMessageAsync(clientId, msg);
        }

        await BroadcastUsersListAsync();

        logger.LogInformation(
            "Client connected: {ClientId} from {IpAddress}",
            clientId,
            ipAddress
        );

        // Step 10: accumulate multi-frame WebSocket messages into a MemoryStream
        using var accum = new MemoryStream();

        try
        {
            byte[] buffer = new byte[4096];

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                bool clientClosedNormally = false;

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
                } while (!result.EndOfMessage);

                if (clientClosedNormally)
                {
                    break;
                }

                if (accum.Length == 0)
                {
                    continue;
                }

                string json = Encoding.UTF8.GetString(accum.ToArray());
                accum.SetLength(0); // reset for next message

                ChatMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<ChatMessage>(json, jsonOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Client {ClientId} sent malformed JSON", clientId);
                    continue;
                }

                if (message is null)
                {
                    continue;
                }

                // Step 13: dispatch via switch expression instead of nested if/else
                await DispatchMessageAsync(clientId, message);
            }
        }
        catch (WebSocketException ex)
            when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            logger.LogWarning("Client {ClientId} disconnected unexpectedly", clientId);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Connection with {ClientId} cancelled", clientId);
        }
        catch (Exception ex)
        {
            // Step 5: single log – HandleError already calls _logger.LogError
            HandleError(clientId, ex, nameof(HandleClientAsync));
        }
        finally
        {
            clientManager.RemoveClient(clientId);
            await BroadcastUsersListAsync();

            if (socket.State == WebSocketState.Open)
            {
                // Step 9: use a fresh token so CloseAsync is never called with
                // an already-cancelled token during server shutdown.
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closed",
                        closeCts.Token
                    );
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error closing WebSocket for {ClientId}", clientId);
                }
            }

            logger.LogInformation("Client disconnected: {ClientId}", clientId);
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

        await (
            message.Type switch
            {
                "findUser" => HandleFindUserAsync(clientId, message),
                "clear" => HandleClearAsync(message),
                "switchChat" => HandleSwitchChatAsync(clientId, message),
                "nickname" => HandleNicknameAsync(clientId, message),
                "typing" => HandleTypingAsync(clientId, message),
                _ => HandleChatMessageAsync(clientId, message),
            }
        );
    }

    private async Task HandleFindUserAsync(string clientId, ChatMessage message) =>
        // Echo back to the requesting client so the client JS can fire the API call
        await SendMessageAsync(clientId, message);

    private async Task HandleClearAsync(ChatMessage message)
    {
        string chatRoomId = message.ChatRoomId ?? DefaultChatRoomId;
        await ClearChatAsync(chatRoomId);
    }

    private async Task HandleSwitchChatAsync(string clientId, ChatMessage message)
    {
        string chatRoomId = message.ChatRoomId ?? DefaultChatRoomId;
        clientManager.UpdateUserCurrentChat(clientId, chatRoomId);

        List<ChatMessage> messages = await GetMessagesByChatAsync(chatRoomId);
        List<OnlineUser> onlineUsers = await GetOnlineUsersByChatAsync(chatRoomId);
        await BroadcastChatUpdateAsync(chatRoomId, messages, onlineUsers);
    }

    private Task HandleNicknameAsync(string clientId, ChatMessage message)
    {
        string nickname = SanitizeNickname(message.Name);
        clientManager.UpdateUserNickname(clientId, nickname);
        return Task.CompletedTask;
    }

    private async Task HandleTypingAsync(string clientId, ChatMessage message)
    {
        string nickname = SanitizeNickname(message.Name);
        clientManager.UpdateUserTypingStatus(clientId, message.IsTyping);
        await BroadcastTypingStatusAsync(clientId, nickname, message.IsTyping);
    }

    private async Task HandleChatMessageAsync(string clientId, ChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        string nickname = SanitizeNickname(message.Name);
        string chatRoomId = message.ChatRoomId ?? DefaultChatRoomId;

        clientManager.UpdateUserNickname(clientId, nickname);

        // Step 7 (SanitizeInput removed): store raw text; client escapes on render
        var chatMessage = ChatMessage.Create(message.Text, nickname, chatRoomId, clientId);
        await messageRepository.AddAsync(chatMessage);
        await BroadcastToChatAsync(chatRoomId, chatMessage, clientId);
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

    private static OnlineUser MapToOnlineUser(ActiveUser u) =>
        new()
        {
            Id = u.Id,
            Nickname = u.Nickname,
            IpAddress = string.Empty, // ip not exposed to clients (Finding 1 – security)
            IsTyping = u.IsTyping,
        };
}
