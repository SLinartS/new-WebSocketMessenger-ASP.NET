using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SimpleMessenger.Models;
using Microsoft.Extensions.Logging;

namespace SimpleMessenger.Services;

public class ChatService : IChatService
{
    private readonly IClientManager _clientManager;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IClientManager clientManager, ILogger<ChatService> logger)
    {
        _clientManager = clientManager;
        _logger = logger;
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

    public async Task HandleClientAsync(string clientId, WebSocket socket, CancellationToken ct)
    {
        _clientManager.AddClient(clientId, socket);
        await BroadcastAsync(ChatMessage.System($"User {clientId} connected"));

        _logger.LogInformation("Client connected: {ClientId}", clientId);

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
                        await BroadcastAsync(
                            ChatMessage.Create(message.Text, message.Name ?? "Anonymous"), 
                            clientId);
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
            await BroadcastAsync(ChatMessage.System($"{clientId} disconnected"));

            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);

            _logger.LogInformation("Client disconnected: {ClientId}", clientId);
        }
    }
}
