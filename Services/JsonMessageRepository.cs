namespace SimpleMessenger.Services;

using System.Text.Json;
using SimpleMessenger.Models;

public class JsonMessageRepository : IMessageRepository, IDisposable
{
    private readonly string _filePath;
    private readonly string _chatRoomsFilePath;
    private readonly string _chatParticipantsFilePath;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<JsonMessageRepository> _logger;

    public JsonMessageRepository(ILogger<JsonMessageRepository> logger)
    {
        this._logger = logger;
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        this._filePath = Path.Combine(directory, "message_history.json");
        this._chatRoomsFilePath = Path.Combine(directory, "chat_rooms.json");
        this._chatParticipantsFilePath = Path.Combine(directory, "chat_participants.json");
    }
    private async Task<T?> LoadJsonFileAsync<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        var json = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            this._logger.LogWarning(ex, "Corrupt JSON file {Path}, returning empty collection", filePath);
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
        await this._semaphore.WaitAsync();
        try
        {
            return await this.LoadJsonFileAsync<List<ChatMessage>>(this._filePath) ?? [];
        }
        finally { this._semaphore.Release(); }
    }

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId)
    {
        var all = await this.GetAllAsync();
        return [.. all.Where(m => m.ChatRoomId == chatRoomId)];
    }

    public async Task AddAsync(ChatMessage message)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var messages = await this.LoadJsonFileAsync<List<ChatMessage>>(this._filePath) ?? [];
            messages.Add(message);
            await SaveJsonFileAsync(this._filePath, messages);
        }
        finally { this._semaphore.Release(); }
    }

    public async Task ClearAsync()
    {
        await this._semaphore.WaitAsync();
        try
        {
            await SaveJsonFileAsync(this._filePath, Array.Empty<ChatMessage>());
        }
        finally { this._semaphore.Release(); }
    }

    public async Task ClearMessagesByChatAsync(string chatRoomId)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var messages = await this.LoadJsonFileAsync<List<ChatMessage>>(this._filePath) ?? [];
            var remaining = messages.Where(m => m.ChatRoomId != chatRoomId).ToList();
            await SaveJsonFileAsync(this._filePath, remaining);
        }
        finally { this._semaphore.Release(); }
    }


    public async Task<List<ChatRoom>> GetChatRoomsAsync()
    {
        await this._semaphore.WaitAsync();
        try
        {
            return await this.LoadJsonFileAsync<List<ChatRoom>>(this._chatRoomsFilePath) ?? [];
        }
        finally { this._semaphore.Release(); }
    }

    public async Task AddChatRoomAsync(ChatRoom chatRoom)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var rooms = await this.LoadJsonFileAsync<List<ChatRoom>>(this._chatRoomsFilePath) ?? [];
            rooms.Add(chatRoom);
            await SaveJsonFileAsync(this._chatRoomsFilePath, rooms);
        }
        finally { this._semaphore.Release(); }
    }

    public async Task RemoveChatRoomAsync(string chatRoomId)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var rooms = await this.LoadJsonFileAsync<List<ChatRoom>>(this._chatRoomsFilePath) ?? [];
            var remaining = rooms.Where(c => c.Id != chatRoomId).ToList();
            await SaveJsonFileAsync(this._chatRoomsFilePath, remaining);
        }
        finally { this._semaphore.Release(); }
    }

    public async Task<List<ChatParticipant>> GetChatParticipantsAsync()
    {
        await this._semaphore.WaitAsync();
        try
        {
            return await this.LoadJsonFileAsync<List<ChatParticipant>>(this._chatParticipantsFilePath) ?? [];
        }
        finally { this._semaphore.Release(); }
    }

    public async Task AddChatParticipantAsync(ChatParticipant participant)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var participants = await this.LoadJsonFileAsync<List<ChatParticipant>>(this._chatParticipantsFilePath) ?? [];
            participants.Add(participant);
            await SaveJsonFileAsync(this._chatParticipantsFilePath, participants);
        }
        finally { this._semaphore.Release(); }
    }

    public async Task RemoveChatParticipantAsync(string chatRoomId, string userId)
    {
        await this._semaphore.WaitAsync();
        try
        {
            var participants = await this.LoadJsonFileAsync<List<ChatParticipant>>(this._chatParticipantsFilePath) ?? [];
            var remaining = participants
                .Where(p => p.ChatRoomId != chatRoomId || p.UserId != userId)
                .ToList();
            await SaveJsonFileAsync(this._chatParticipantsFilePath, remaining);
        }
        finally { this._semaphore.Release(); }
    }

    public async Task<List<ChatParticipant>> GetParticipantsByChatAsync(string chatRoomId)
    {
        var all = await this.GetChatParticipantsAsync();
        return [.. all.Where(p => p.ChatRoomId == chatRoomId)];
    }

    public async Task<bool> IsUserInChatAsync(string chatRoomId, string userId)
    {
        var participants = await this.GetParticipantsByChatAsync(chatRoomId);
        return participants.Any(p => p.UserId == userId);
    }

    public void Dispose()
    {
        this._semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
