namespace SimpleMessenger.Models;

public class ChatRoom
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsPrivate { get; set; }
}
