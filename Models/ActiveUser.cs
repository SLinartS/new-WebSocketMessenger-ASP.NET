namespace SimpleMessenger.Models;

public class ActiveUser
{
    public string Id { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public bool IsTyping { get; set; }
    public string CurrentChatId { get; set; } = "general";
}
