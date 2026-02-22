using System.Text.Json.Serialization;

namespace SimpleMessenger.Models;

public class ChatMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("chatRoomId")]
    public string ChatRoomId { get; set; } = "general";

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; } = "Anonymous";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("isTyping")]

    public bool IsTyping { get; set; }

    public static ChatMessage Create(string text, string name, string chatRoomId = "general", string? userId = null) =>
        new() { Text = text, Name = name, Type = "message", ChatRoomId = chatRoomId, UserId = userId };

    public static ChatMessage System(string text, string chatRoomId = "general") =>
        new() { Text = text, Type = "system", ChatRoomId = chatRoomId };

    public static ChatMessage Clear(string text, string chatRoomId = "general") =>
        new() { Text = text, Type = "clear", ChatRoomId = chatRoomId };
}
