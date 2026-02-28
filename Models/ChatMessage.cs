
using System.Text.Json.Serialization;

namespace SimpleMessenger.Models;

public class ChatMessage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("chatRoomId")]
    public string ChatRoomId { get; init; } = "general";

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; } = "Anonymous";

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "message";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; init; }

    public static ChatMessage Create(
        string text,
        string name,
        string chatRoomId = "general",
        string? userId = null
    ) =>
        new()
        {
            Text = text,
            Name = name,
            Type = "message",
            ChatRoomId = chatRoomId,
            UserId = userId,
        };

    public static ChatMessage System(string text, string chatRoomId = "general") =>
        new()
        {
            Text = text,
            Type = "system",
            ChatRoomId = chatRoomId,
        };

    public static ChatMessage Clear(string text, string chatRoomId = "general") =>
        new()
        {
            Text = text,
            Type = "clear",
            ChatRoomId = chatRoomId,
        };
}
