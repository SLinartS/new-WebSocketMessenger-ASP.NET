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

    private async Task<T?> LoadJsonFileAsync<T>(string filePath)
    {
        if (!File.Exists(filePath))
            return default;

        var json = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json);
    }

    private async Task SaveJsonFileAsync<T>(string filePath, T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await Task.Run(() =>
        {
            lock (_lock)
            {
                File.WriteAllText(filePath, json);
            }
        });
    }

    public async Task<List<ChatMessage>> GetAllAsync()
    {
        return await LoadJsonFileAsync<List<ChatMessage>>(_filePath) ?? new List<ChatMessage>();
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
        await SaveJsonFileAsync(_filePath, messages);
    }

    public async Task ClearAsync()
    {
        await SaveJsonFileAsync(_filePath, new List<ChatMessage>());
    }

    public async Task ClearMessagesByChatAsync(string chatRoomId)
    {
        var messages = await GetAllAsync();
        var remainingMessages = messages.Where(m => m.ChatRoomId != chatRoomId).ToList();
        await SaveJsonFileAsync(_filePath, remainingMessages);
    }

    public async Task<List<ChatRoom>> GetChatRoomsAsync()
    {
        return await LoadJsonFileAsync<List<ChatRoom>>(_chatRoomsFilePath) ?? new List<ChatRoom>();
    }

    public async Task AddChatRoomAsync(ChatRoom chatRoom)
    {
        var chatRooms = await GetChatRoomsAsync();
        chatRooms.Add(chatRoom);
        await SaveJsonFileAsync(_chatRoomsFilePath, chatRooms);
    }

    public async Task RemoveChatRoomAsync(string chatRoomId)
    {
        var chatRooms = await GetChatRoomsAsync();
        var remainingChatRooms = chatRooms.Where(c => c.Id != chatRoomId).ToList();
        await SaveJsonFileAsync(_chatRoomsFilePath, remainingChatRooms);
    }

    public async Task<List<ChatParticipant>> GetChatParticipantsAsync()
    {
        return await LoadJsonFileAsync<List<ChatParticipant>>(_chatParticipantsFilePath) ?? new List<ChatParticipant>();
    }

    public async Task AddChatParticipantAsync(ChatParticipant participant)
    {
        var participants = await GetChatParticipantsAsync();
        participants.Add(participant);
        await SaveJsonFileAsync(_chatParticipantsFilePath, participants);
    }

    public async Task RemoveChatParticipantAsync(string chatRoomId, string userId)
    {
        var participants = await GetChatParticipantsAsync();
        var remainingParticipants = participants.Where(p => p.ChatRoomId != chatRoomId || p.UserId != userId).ToList();
        await SaveJsonFileAsync(_chatParticipantsFilePath, remainingParticipants);
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
