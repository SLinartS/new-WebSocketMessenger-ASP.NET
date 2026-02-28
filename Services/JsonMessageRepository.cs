using System.Text.Json;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class JsonMessageRepository : IMessageRepository
{
    private readonly string _filePath;
    private readonly string _chatRoomsFilePath;
    private readonly string _chatParticipantsFilePath;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<JsonMessageRepository> _logger;

    public JsonMessageRepository(ILogger<JsonMessageRepository> logger)
    {
        _logger = logger;
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(directory, "message_history.json");
        _chatRoomsFilePath = Path.Combine(directory, "chat_rooms.json");
        _chatParticipantsFilePath = Path.Combine(directory, "chat_participants.json");
    }
    private async Task<T?> LoadJsonFileAsync<T>(string filePath)
    {
        if (!File.Exists(filePath)) return default;

        var json = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(json)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupt JSON file {Path}, returning empty collection", filePath);
            return default;
        }
    }

    private static async Task SaveJsonFileAsync<T>(string filePath, T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<ChatMessage>> GetAllAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadJsonFileAsync<List<ChatMessage>>(_filePath) ?? [];
        }
        finally { _semaphore.Release(); }
    }

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId)
    {
        var all = await GetAllAsync();
        return all.Where(m => m.ChatRoomId == chatRoomId).ToList();
    }

    public async Task AddAsync(ChatMessage message)
    {
        await _semaphore.WaitAsync();
        try
        {
            var messages = await LoadJsonFileAsync<List<ChatMessage>>(_filePath) ?? [];
            messages.Add(message);
            await SaveJsonFileAsync(_filePath, messages);
        }
        finally { _semaphore.Release(); }
    }

    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            await SaveJsonFileAsync(_filePath, Array.Empty<ChatMessage>());
        }
        finally { _semaphore.Release(); }
    }

    public async Task ClearMessagesByChatAsync(string chatRoomId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var messages = await LoadJsonFileAsync<List<ChatMessage>>(_filePath) ?? [];
            var remaining = messages.Where(m => m.ChatRoomId != chatRoomId).ToList();
            await SaveJsonFileAsync(_filePath, remaining);
        }
        finally { _semaphore.Release(); }
    }


    public async Task<List<ChatRoom>> GetChatRoomsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadJsonFileAsync<List<ChatRoom>>(_chatRoomsFilePath) ?? [];
        }
        finally { _semaphore.Release(); }
    }

    public async Task AddChatRoomAsync(ChatRoom chatRoom)
    {
        await _semaphore.WaitAsync();
        try
        {
            var rooms = await LoadJsonFileAsync<List<ChatRoom>>(_chatRoomsFilePath) ?? [];
            rooms.Add(chatRoom);
            await SaveJsonFileAsync(_chatRoomsFilePath, rooms);
        }
        finally { _semaphore.Release(); }
    }

    public async Task RemoveChatRoomAsync(string chatRoomId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var rooms = await LoadJsonFileAsync<List<ChatRoom>>(_chatRoomsFilePath) ?? [];
            var remaining = rooms.Where(c => c.Id != chatRoomId).ToList();
            await SaveJsonFileAsync(_chatRoomsFilePath, remaining);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<List<ChatParticipant>> GetChatParticipantsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return await LoadJsonFileAsync<List<ChatParticipant>>(_chatParticipantsFilePath) ?? [];
        }
        finally { _semaphore.Release(); }
    }

    public async Task AddChatParticipantAsync(ChatParticipant participant)
    {
        await _semaphore.WaitAsync();
        try
        {
            var participants = await LoadJsonFileAsync<List<ChatParticipant>>(_chatParticipantsFilePath) ?? [];
            participants.Add(participant);
            await SaveJsonFileAsync(_chatParticipantsFilePath, participants);
        }
        finally { _semaphore.Release(); }
    }

    public async Task RemoveChatParticipantAsync(string chatRoomId, string userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var participants = await LoadJsonFileAsync<List<ChatParticipant>>(_chatParticipantsFilePath) ?? [];
            var remaining = participants
                .Where(p => p.ChatRoomId != chatRoomId || p.UserId != userId)
                .ToList();
            await SaveJsonFileAsync(_chatParticipantsFilePath, remaining);
        }
        finally { _semaphore.Release(); }
    }

    public async Task<List<ChatParticipant>> GetParticipantsByChatAsync(string chatRoomId)
    {
        var all = await GetChatParticipantsAsync();
        return all.Where(p => p.ChatRoomId == chatRoomId).ToList();
    }

    public async Task<bool> IsUserInChatAsync(string chatRoomId, string userId)
    {
        var participants = await GetParticipantsByChatAsync(chatRoomId);
        return participants.Any(p => p.UserId == userId);
    }
}
