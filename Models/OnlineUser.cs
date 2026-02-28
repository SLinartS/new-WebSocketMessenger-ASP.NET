namespace SimpleMessenger.Models;

public class OnlineUser
{
    public string Id { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
}
