namespace SimpleMessenger.Models;

public class ChatParticipant
{
    public string ChatRoomId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
