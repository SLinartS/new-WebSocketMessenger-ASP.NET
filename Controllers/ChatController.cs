using Microsoft.AspNetCore.Mvc;
using SimpleMessenger.Models;
using SimpleMessenger.Services;

namespace SimpleMessenger.Controllers;

[ApiController]
[Route("api/chats")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IClientManager _clientManager;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, IClientManager clientManager, IMessageRepository messageRepository, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _clientManager = clientManager;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    [HttpGet("{chatId}/messages")]
    public async Task<ActionResult<List<ChatMessage>>> GetMessages(string chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest("ChatId is required");
        }

        var messages = await _messageRepository.GetMessagesByChatAsync(chatId);
        return Ok(messages);
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatRoom>>> GetChats()
    {
        var chatRooms = await _messageRepository.GetChatRoomsAsync();
        return Ok(chatRooms);
    }

    [HttpPost]
    public async Task<ActionResult<ChatRoom>> CreateChat([FromBody] CreateChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("UserId is required");
        }

        if (string.IsNullOrWhiteSpace(request.TargetUserId))
        {
            // Create general chat if no target user specified
            var generalChat = new ChatRoom
            {
                Id = "general",
                Name = "Общий чат",
                CreatedAt = DateTime.UtcNow,
                IsPrivate = false
            };

            var chatRooms = await _messageRepository.GetChatRoomsAsync();
            if (!chatRooms.Any(c => c.Id == "general"))
            {
                await _messageRepository.AddChatRoomAsync(generalChat);
            }

            return Ok(generalChat);
        }

        // Check if private chat already exists
        var existingChat = await GetPrivateChatAsync(request.UserId, request.TargetUserId);
        if (existingChat != null)
        {
            return Ok(existingChat);
        }

        // Create new private chat
        var chatId = $"chat_{Guid.NewGuid().ToString().Substring(0, 8)}";
        var chatRoom = new ChatRoom
        {
            Id = chatId,
            Name = $"Приватный чат",
            CreatedAt = DateTime.UtcNow,
            IsPrivate = true
        };

        await _messageRepository.AddChatRoomAsync(chatRoom);
        await _messageRepository.AddChatParticipantAsync(new ChatParticipant
        {
            ChatRoomId = chatId,
            UserId = request.UserId,
            JoinedAt = DateTime.UtcNow
        });
        await _messageRepository.AddChatParticipantAsync(new ChatParticipant
        {
            ChatRoomId = chatId,
            UserId = request.TargetUserId,
            JoinedAt = DateTime.UtcNow
        });

        _logger.LogInformation("Created private chat {ChatId} between {User1} and {User2}", 
            chatId, request.UserId, request.TargetUserId);

        return Ok(chatRoom);
    }

    [HttpDelete("{chatId}")]
    public async Task<IActionResult> DeleteChat(string chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest("ChatId is required");
        }

        // Remove chat room
        await _messageRepository.RemoveChatRoomAsync(chatId);

        // Remove all participants
        var participants = await _messageRepository.GetParticipantsByChatAsync(chatId);
        foreach (var participant in participants)
        {
            await _messageRepository.RemoveChatParticipantAsync(chatId, participant.UserId);
        }

        // Remove all messages in this chat
        await _messageRepository.ClearMessagesByChatAsync(chatId);

        _logger.LogInformation("Deleted chat {ChatId}", chatId);

        return NoContent();
    }

    [HttpPost("{chatId}/switch")]
    public async Task<IActionResult> SwitchChat(string chatId, [FromBody] SwitchChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("ChatId and UserId are required");
        }

        // Check if user is participant in this chat
        var isParticipant = await _messageRepository.IsUserInChatAsync(chatId, request.UserId);
        if (!isParticipant)
        {
            return BadRequest("User is not participant in this chat");
        }

        // Update user's current chat
        _clientManager.UpdateUserCurrentChat(request.UserId, chatId);

        // Get messages for this chat
        var messages = await _messageRepository.GetMessagesByChatAsync(chatId);

        // Get participants for this chat (online users)
        var participants = await _messageRepository.GetParticipantsByChatAsync(chatId);
        var onlineUsers = _clientManager.GetActiveUsers()
            .Where(u => participants.Any(p => p.UserId == u.Id))
            .Select(u => new OnlineUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IpAddress = u.IpAddress,
                IsTyping = u.IsTyping
            }).ToList();

        // Broadcast to all participants in the chat
        await _chatService.BroadcastChatUpdateAsync(chatId, messages, onlineUsers);

        return Ok(new { success = true });
    }

    [HttpPost("find-user")]
    public async Task<ActionResult<UserSearchResult>> FindUser([FromBody] FindUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("UserId is required");
        }

        var activeUsers = _clientManager.GetActiveUsers().ToList();
        var foundUser = activeUsers.FirstOrDefault(u => u.Id == request.TargetUserId);

        if (foundUser == null)
        {
            return Ok(new UserSearchResult { Found = false, Message = "User not found" });
        }

        return Ok(new UserSearchResult 
        { 
            Found = true, 
            User = new UserInfo
            {
                Id = foundUser.Id,
                Nickname = foundUser.Nickname,
                IpAddress = foundUser.IpAddress
            }
        });
    }

    private async Task<ChatRoom?> GetPrivateChatAsync(string userId1, string userId2)
    {
        var chatRooms = await _messageRepository.GetChatRoomsAsync();
        var participants = await _messageRepository.GetChatParticipantsAsync();

        foreach (var chatRoom in chatRooms.Where(c => c.IsPrivate))
        {
            var chatParticipants = participants.Where(p => p.ChatRoomId == chatRoom.Id).Select(p => p.UserId).ToList();
            if (chatParticipants.Contains(userId1) && chatParticipants.Contains(userId2))
            {
                return chatRoom;
            }
        }

        return null;
    }
}

public class CreateChatRequest
{
    public string? UserId { get; set; }
    public string? TargetUserId { get; set; }
}

public class SwitchChatRequest
{
    public string? UserId { get; set; }
}

public class FindUserRequest
{
    public string? UserId { get; set; }
    public string? TargetUserId { get; set; }
}

public class UserSearchResult
{
    public bool Found { get; set; }
    public UserInfo? User { get; set; }
    public string? Message { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
