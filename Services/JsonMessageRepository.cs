using System.Text.Json;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class JsonMessageRepository : IMessageRepository
{
    private readonly string _filePath;
    private readonly string _chatRoomsFilePath;
    private readonly string _chatParticipantsFilePath;
    private readonly Lock _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonMessageRepository()
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(directory, "message_history.json");
        _chatRoomsFilePath = Path.Combine(directory, "chat_rooms.json");
        _chatParticipantsFilePath = Path.Combine(directory, "chat_participants.json");
    }

    public async Task<List<ChatMessage>> GetAllAsync()
    {
        if (!File.Exists(_filePath))
            return new List<ChatMessage>();

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<ChatMessage>();

        return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
    }

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId)
    {
        var allMessages = await GetAllAsync();
        return allMessages.Where(m => m.ChatRoomId == chatRoomId).ToList();
    }

    public async Task AddAsync(ChatMessage message)
    {
        var messages = await GetAllAsync();
        messages.Add(message);

        var json = JsonSerializer.Serialize(messages, JsonOptions);

        await Task.Run(() =>
        {
            lock (_lock)
            {
                File.WriteAllText(_filePath, json);
            }
        });
    }

    public async Task ClearAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (File.Exists(_filePath))
                {
                    File.WriteAllText(_filePath, "[]");
                }
            }
        });
    }

    public async Task ClearMessagesByChatAsync(string chatRoomId)
    {
        var messages = await GetAllAsync();
        var remainingMessages = messages.Where(m => m.ChatRoomId != chatRoomId).ToList();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(remainingMessages, JsonOptions);
                File.WriteAllText(_filePath, json);
            }
        });
    }

    public async Task<List<ChatRoom>> GetChatRoomsAsync()
    {
        if (!File.Exists(_chatRoomsFilePath))
            return new List<ChatRoom>();

        var json = await File.ReadAllTextAsync(_chatRoomsFilePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<ChatRoom>();

        return JsonSerializer.Deserialize<List<ChatRoom>>(json) ?? new List<ChatRoom>();
    }

    public async Task AddChatRoomAsync(ChatRoom chatRoom)
    {
        var chatRooms = await GetChatRoomsAsync();
        chatRooms.Add(chatRoom);

        var json = JsonSerializer.Serialize(chatRooms, JsonOptions);

        await Task.Run(() =>
        {
            lock (_lock)
            {
                File.WriteAllText(_chatRoomsFilePath, json);
            }
        });
    }

    public async Task RemoveChatRoomAsync(string chatRoomId)
    {
        var chatRooms = await GetChatRoomsAsync();
        var remainingChatRooms = chatRooms.Where(c => c.Id != chatRoomId).ToList();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(remainingChatRooms, JsonOptions);
                File.WriteAllText(_chatRoomsFilePath, json);
            }
        });
    }

    public async Task<List<ChatParticipant>> GetChatParticipantsAsync()
    {
        if (!File.Exists(_chatParticipantsFilePath))
            return new List<ChatParticipant>();

        var json = await File.ReadAllTextAsync(_chatParticipantsFilePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<ChatParticipant>();

        return JsonSerializer.Deserialize<List<ChatParticipant>>(json) ?? new List<ChatParticipant>();
    }

    public async Task AddChatParticipantAsync(ChatParticipant participant)
    {
        var participants = await GetChatParticipantsAsync();
        participants.Add(participant);

        var json = JsonSerializer.Serialize(participants, JsonOptions);

        await Task.Run(() =>
        {
            lock (_lock)
            {
                File.WriteAllText(_chatParticipantsFilePath, json);
            }
        });
    }

    public async Task RemoveChatParticipantAsync(string chatRoomId, string userId)
    {
        var participants = await GetChatParticipantsAsync();
        var remainingParticipants = participants.Where(p => p.ChatRoomId != chatRoomId || p.UserId != userId).ToList();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(remainingParticipants, JsonOptions);
                File.WriteAllText(_chatParticipantsFilePath, json);
            }
        });
    }

    public async Task<List<ChatParticipant>> GetParticipantsByChatAsync(string chatRoomId)
    {
        var allParticipants = await GetChatParticipantsAsync();
        return allParticipants.Where(p => p.ChatRoomId == chatRoomId).ToList();
    }

    public async Task<bool> IsUserInChatAsync(string chatRoomId, string userId)
    {
        var participants = await GetParticipantsByChatAsync(chatRoomId);
        return participants.Any(p => p.UserId == userId);
    }
}
