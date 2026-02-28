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

    public ChatController(
        IChatService chatService,
        IClientManager clientManager,
        IMessageRepository messageRepository,
        ILogger<ChatController> logger)
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
            return BadRequest("ChatId is required");

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
            return BadRequest("UserId is required");

        if (string.IsNullOrWhiteSpace(request.TargetUserId))
        {
            // No target user → ensure the general chat exists and return it
            var generalChat = new ChatRoom
            {
                Id = "general",
                Name = "Общий чат",
                CreatedAt = DateTime.UtcNow,
                IsPrivate = false
            };

            var chatRooms = await _messageRepository.GetChatRoomsAsync();
            if (!chatRooms.Any(c => c.Id == "general"))
                await _messageRepository.AddChatRoomAsync(generalChat);

            return Ok(generalChat);
        }

        // Return existing private chat if one already exists between these two users
        var existingChat = await FindPrivateChatAsync(request.UserId, request.TargetUserId);
        if (existingChat != null)
            return Ok(existingChat);

        // Create a new private chat
        var chatId = $"chat_{Guid.NewGuid().ToString()[..8]}";
        var chatRoom = new ChatRoom
        {
            Id = chatId,
            Name = "Приватный чат",
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
    public async Task<IActionResult> DeleteChat(string chatId, [FromQuery] string? userId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            return BadRequest("ChatId is required");

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var isParticipant = await _messageRepository.IsUserInChatAsync(chatId, userId);
            if (!isParticipant)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to delete chat {ChatId} without being a participant",
                    userId,
                    chatId
                );
                return Forbid();
            }
        }

        await _messageRepository.RemoveChatRoomAsync(chatId);

        var participants = await _messageRepository.GetParticipantsByChatAsync(chatId);
        foreach (var participant in participants)
            await _messageRepository.RemoveChatParticipantAsync(chatId, participant.UserId);

        await _messageRepository.ClearMessagesByChatAsync(chatId);

        _logger.LogInformation("Deleted chat {ChatId}", chatId);
        return NoContent();
    }

    [HttpPost("{chatId}/switch")]
    public async Task<IActionResult> SwitchChat(string chatId, [FromBody] SwitchChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest("ChatId and UserId are required");

        var isParticipant = await _messageRepository.IsUserInChatAsync(chatId, request.UserId);
        if (!isParticipant)
            return BadRequest("User is not participant in this chat");

        _clientManager.UpdateUserCurrentChat(request.UserId, chatId);

        var messages = await _messageRepository.GetMessagesByChatAsync(chatId);

        var participants = await _messageRepository.GetParticipantsByChatAsync(chatId);
        var onlineUsers = _clientManager.GetActiveUsers()
            .Where(u => participants.Any(p => p.UserId == u.Id))
            .Select(u => new OnlineUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IsTyping = u.IsTyping
            }).ToList();

        await _chatService.BroadcastChatUpdateAsync(chatId, messages, onlineUsers);

        return Ok(new { success = true });
    }

    [HttpPost("find-user")]
    public Task<ActionResult<UserSearchResult>> FindUser([FromBody] FindUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
            return Task.FromResult<ActionResult<UserSearchResult>>(BadRequest("UserId is required"));

        var foundUser = _clientManager.GetActiveUsers()
            .FirstOrDefault(u => u.Id == request.TargetUserId);

        if (foundUser is null)
        {
            return Task.FromResult<ActionResult<UserSearchResult>>(
                Ok(new UserSearchResult { Found = false, Message = "User not found" })
            );
        }

        return Task.FromResult<ActionResult<UserSearchResult>>(Ok(new UserSearchResult
        {
            Found = true,
            User = new UserInfo
            {
                Id = foundUser.Id,
                Nickname = foundUser.Nickname
            }
        }));
    }

    private async Task<ChatRoom?> FindPrivateChatAsync(string userId1, string userId2)
    {
        var chatRooms = await _messageRepository.GetChatRoomsAsync();

        foreach (var room in chatRooms.Where(c => c.IsPrivate))
        {
            var roomParticipants = await _messageRepository.GetParticipantsByChatAsync(room.Id);
            var ids = roomParticipants.Select(p => p.UserId).ToHashSet();
            if (ids.Contains(userId1) && ids.Contains(userId2))
                return room;
        }

        return null;
    }
}
