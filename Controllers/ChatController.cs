
using Microsoft.AspNetCore.Mvc;
using SimpleMessenger.Models;
using SimpleMessenger.Services;

namespace SimpleMessenger.Controllers;

[ApiController]
[Route("api/chats")]
public class ChatController(
    IChatService chatService,
    IClientManager clientManager,
    IMessageRepository messageRepository,
    ILogger<ChatController> logger
) : ControllerBase
{
    private readonly IChatService chatService = chatService;
    private readonly IClientManager clientManager = clientManager;
    private readonly IMessageRepository messageRepository = messageRepository;
    private readonly ILogger<ChatController> logger = logger;

    [HttpGet("{chatId}/messages")]
    public async Task<ActionResult<List<ChatMessage>>> GetMessages(string chatId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest("ChatId is required");
        }

        List<ChatMessage> messages = await messageRepository.GetMessagesByChatAsync(chatId);
        return Ok(messages);
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatRoom>>> GetChats()
    {
        List<ChatRoom> chatRooms = await messageRepository.GetChatRoomsAsync();
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
            // No target user → ensure the general chat exists and return it
            var generalChat = new ChatRoom
            {
                Id = "general",
                Name = "Общий чат",
                CreatedAt = DateTime.UtcNow,
                IsPrivate = false,
            };

            List<ChatRoom> chatRooms = await messageRepository.GetChatRoomsAsync();
            if (!chatRooms.Any(c => c.Id == "general"))
            {
                await messageRepository.AddChatRoomAsync(generalChat);
            }

            return Ok(generalChat);
        }

        // Return existing private chat if one already exists between these two users
        ChatRoom? existingChat = await FindPrivateChatAsync(request.UserId, request.TargetUserId);
        if (existingChat != null)
        {
            return Ok(existingChat);
        }

        // Create a new private chat
        string chatId = $"chat_{Guid.NewGuid().ToString()[..8]}";
        var chatRoom = new ChatRoom
        {
            Id = chatId,
            Name = "Приватный чат",
            CreatedAt = DateTime.UtcNow,
            IsPrivate = true,
        };

        await messageRepository.AddChatRoomAsync(chatRoom);
        await messageRepository.AddChatParticipantAsync(
            new ChatParticipant
            {
                ChatRoomId = chatId,
                UserId = request.UserId,
                JoinedAt = DateTime.UtcNow,
            }
        );
        await messageRepository.AddChatParticipantAsync(
            new ChatParticipant
            {
                ChatRoomId = chatId,
                UserId = request.TargetUserId,
                JoinedAt = DateTime.UtcNow,
            }
        );

        logger.LogInformation(
            "Created private chat {ChatId} between {User1} and {User2}",
            chatId,
            request.UserId,
            request.TargetUserId
        );

        return Ok(chatRoom);
    }

    [HttpDelete("{chatId}")]
    public async Task<IActionResult> DeleteChat(string chatId, [FromQuery] string? userId)
    {
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return BadRequest("ChatId is required");
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            bool isParticipant = await messageRepository.IsUserInChatAsync(chatId, userId);
            if (!isParticipant)
            {
                logger.LogWarning(
                    "User {UserId} attempted to delete chat {ChatId} without being a participant",
                    userId,
                    chatId
                );
                return Forbid();
            }
        }

        await messageRepository.RemoveChatRoomAsync(chatId);

        List<ChatParticipant> participants = await messageRepository.GetParticipantsByChatAsync(chatId);
        foreach (ChatParticipant participant in participants)
        {
            await messageRepository.RemoveChatParticipantAsync(chatId, participant.UserId);
        }

        await messageRepository.ClearMessagesByChatAsync(chatId);

        logger.LogInformation("Deleted chat {ChatId}", chatId);
        return NoContent();
    }

    [HttpPost("{chatId}/switch")]
    public async Task<IActionResult> SwitchChat(string chatId, [FromBody] SwitchChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("ChatId and UserId are required");
        }

        bool isParticipant = await messageRepository.IsUserInChatAsync(chatId, request.UserId);
        if (!isParticipant)
        {
            return BadRequest("User is not participant in this chat");
        }

        clientManager.UpdateUserCurrentChat(request.UserId, chatId);

        List<ChatMessage> messages = await messageRepository.GetMessagesByChatAsync(chatId);

        List<ChatParticipant> participants = await messageRepository.GetParticipantsByChatAsync(chatId);
        var onlineUsers =
            clientManager.GetActiveUsers()
            .Where(u => participants.Any(p => p.UserId == u.Id))
            .Select(u => new OnlineUser
            {
                Id = u.Id,
                Nickname = u.Nickname,
                IsTyping = u.IsTyping,
            })
            .ToList();

        await chatService.BroadcastChatUpdateAsync(chatId, messages, onlineUsers);

        return Ok(new { success = true });
    }

    [HttpPost("find-user")]
    public Task<ActionResult<UserSearchResult>> FindUser([FromBody] FindUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return Task.FromResult<ActionResult<UserSearchResult>>(
                BadRequest("UserId is required")
            );
        }

        ActiveUser? foundUser =
            clientManager.GetActiveUsers()
            .FirstOrDefault(u => u.Id == request.TargetUserId);

        if (foundUser is null)
        {
            return Task.FromResult<ActionResult<UserSearchResult>>(
                Ok(new UserSearchResult { Found = false, Message = "User not found" })
            );
        }

        return Task.FromResult<ActionResult<UserSearchResult>>(
            Ok(
                new UserSearchResult
                {
                    Found = true,
                    User = new UserInfo { Id = foundUser.Id, Nickname = foundUser.Nickname },
                }
            )
        );
    }

    private async Task<ChatRoom?> FindPrivateChatAsync(string userId1, string userId2)
    {
        List<ChatRoom> chatRooms = await messageRepository.GetChatRoomsAsync();

        foreach (ChatRoom? room in chatRooms.Where(c => c.IsPrivate))
        {
            List<ChatParticipant> roomParticipants = await messageRepository.GetParticipantsByChatAsync(room.Id);
            var ids = roomParticipants.Select(p => p.UserId).ToHashSet();
            if (ids.Contains(userId1) && ids.Contains(userId2))
            {
                return room;
            }
        }

        return null;
    }
}
