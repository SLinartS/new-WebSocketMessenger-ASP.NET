namespace SimpleMessenger.Controllers;

using Microsoft.AspNetCore.Mvc;
using SimpleMessenger.Models;
using SimpleMessenger.Services;

[ApiController]
[Route("api/chats")]
public class ChatController(
    IChatService chatService,
    IClientManager clientManager,
    IMessageRepository messageRepository,
    ILogger<ChatController> logger) : ControllerBase
{
    private readonly IChatService _chatService = chatService;
    private readonly IClientManager _clientManager = clientManager;
    private readonly IMessageRepository _messageRepository = messageRepository;
    private readonly ILogger<ChatController> _logger = logger;

    [HttpGet("{chatId}/messages")]
    public async Task<ActionResult<List<ChatMessage>>> GetMessages(string chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return this.BadRequest("ChatId is required");
        }

        var messages = await this._messageRepository.GetMessagesByChatAsync(chatId);
        return this.Ok(messages);
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatRoom>>> GetChats()
    {
        var chatRooms = await this._messageRepository.GetChatRoomsAsync();
        return this.Ok(chatRooms);
    }

    [HttpPost]
    public async Task<ActionResult<ChatRoom>> CreateChat([FromBody] CreateChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return this.BadRequest("UserId is required");
        }

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

            var chatRooms = await this._messageRepository.GetChatRoomsAsync();
            if (!chatRooms.Any(c => c.Id == "general"))
            {
                await this._messageRepository.AddChatRoomAsync(generalChat);
            }

            return this.Ok(generalChat);
        }

        // Return existing private chat if one already exists between these two users
        var existingChat = await this.FindPrivateChatAsync(request.UserId, request.TargetUserId);
        if (existingChat != null)
        {
            return this.Ok(existingChat);
        }

        // Create a new private chat
        var chatId = $"chat_{Guid.NewGuid().ToString()[..8]}";
        var chatRoom = new ChatRoom
        {
            Id = chatId,
            Name = "Приватный чат",
            CreatedAt = DateTime.UtcNow,
            IsPrivate = true
        };

        await this._messageRepository.AddChatRoomAsync(chatRoom);
        await this._messageRepository.AddChatParticipantAsync(new ChatParticipant
        {
            ChatRoomId = chatId,
            UserId = request.UserId,
            JoinedAt = DateTime.UtcNow
        });
        await this._messageRepository.AddChatParticipantAsync(new ChatParticipant
        {
            ChatRoomId = chatId,
            UserId = request.TargetUserId,
            JoinedAt = DateTime.UtcNow
        });

        this._logger.LogInformation("Created private chat {ChatId} between {User1} and {User2}",
            chatId, request.UserId, request.TargetUserId);

        return this.Ok(chatRoom);
    }

    [HttpDelete("{chatId}")]
    public async Task<IActionResult> DeleteChat(string chatId, [FromQuery] string? userId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return this.BadRequest("ChatId is required");
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var isParticipant = await this._messageRepository.IsUserInChatAsync(chatId, userId);
            if (!isParticipant)
            {
                this._logger.LogWarning(
                    "User {UserId} attempted to delete chat {ChatId} without being a participant",
                    userId,
                    chatId
                );
                return this.Forbid();
            }
        }

        await this._messageRepository.RemoveChatRoomAsync(chatId);

        var participants = await this._messageRepository.GetParticipantsByChatAsync(chatId);
        foreach (var participant in participants)
        {
            await this._messageRepository.RemoveChatParticipantAsync(chatId, participant.UserId);
        }

        await this._messageRepository.ClearMessagesByChatAsync(chatId);

        this._logger.LogInformation("Deleted chat {ChatId}", chatId);
        return this.NoContent();
    }

    [HttpPost("{chatId}/switch")]
    public async Task<IActionResult> SwitchChat(string chatId, [FromBody] SwitchChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(request.UserId))
        {
            return this.BadRequest("ChatId and UserId are required");
        }

        var isParticipant = await this._messageRepository.IsUserInChatAsync(chatId, request.UserId);
        if (!isParticipant)
        {
            return this.BadRequest("User is not participant in this chat");
        }

        this._clientManager.UpdateUserCurrentChat(request.UserId, chatId);

        var messages = await this._messageRepository.GetMessagesByChatAsync(chatId);

        var participants = await this._messageRepository.GetParticipantsByChatAsync(chatId);
        var onlineUsers = this._clientManager.GetActiveUsers()
            .Where(u => participants.Any(p => p.UserId == u.Id))
            .Select(u => new OnlineUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IsTyping = u.IsTyping
            }).ToList();

        await this._chatService.BroadcastChatUpdateAsync(chatId, messages, onlineUsers);

        return this.Ok(new { success = true });
    }

    [HttpPost("find-user")]
    public Task<ActionResult<UserSearchResult>> FindUser([FromBody] FindUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return Task.FromResult<ActionResult<UserSearchResult>>(this.BadRequest("UserId is required"));
        }

        var foundUser = this._clientManager.GetActiveUsers()
            .FirstOrDefault(u => u.Id == request.TargetUserId);

        if (foundUser is null)
        {
            return Task.FromResult<ActionResult<UserSearchResult>>(
                this.Ok(new UserSearchResult { Found = false, Message = "User not found" })
            );
        }

        return Task.FromResult<ActionResult<UserSearchResult>>(this.Ok(new UserSearchResult
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
        var chatRooms = await this._messageRepository.GetChatRoomsAsync();

        foreach (var room in chatRooms.Where(c => c.IsPrivate))
        {
            var roomParticipants = await this._messageRepository.GetParticipantsByChatAsync(room.Id);
            var ids = roomParticipants.Select(p => p.UserId).ToHashSet();
            if (ids.Contains(userId1) && ids.Contains(userId2))
            {
                return room;
            }
        }

        return null;
    }
}
