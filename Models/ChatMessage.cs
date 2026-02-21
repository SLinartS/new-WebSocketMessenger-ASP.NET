using System.Text.Json.Serialization;

namespace SimpleMessenger.Models;

public class ChatMessage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = "Anonymous";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; set; }

    public static ChatMessage Create(string text, string name) =>
        new() { Text = text, Name = name, Type = "message" };

    public static ChatMessage System(string text) =>
        new() { Text = text, Type = "system" };
}
