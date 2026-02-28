namespace SimpleMessenger.Services;

using System.Text.Json;
using SimpleMessenger.Models;

public class JsonMessageRepository : IMessageRepository, IDisposable
{
    private readonly string filePath;
    private readonly string chatRoomsFilePath;
    private readonly string chatParticipantsFilePath;

    private readonly SemaphoreSlim semaphore = new(1, 1);

    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    private readonly ILogger<JsonMessageRepository> logger;

    public JsonMessageRepository(ILogger<JsonMessageRepository> logger)
    {
        this.logger = logger;
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        this.filePath = Path.Combine(directory, "message_history.json");
        this.chatRoomsFilePath = Path.Combine(directory, "chat_rooms.json");
        this.chatParticipantsFilePath = Path.Combine(directory, "chat_participants.json");
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
            this.logger.LogWarning(
                ex,
                "Corrupt JSON file {Path}, returning empty collection",
                filePath
            );
            return default;
        }
    }

    private static async Task SaveJsonFileAsync<T>(string filePath, T data)
    {
        var json = JsonSerializer.Serialize(data, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<ChatMessage>> GetAllAsync()
    {
        await this.semaphore.WaitAsync();
        try
        {
            return await this.LoadJsonFileAsync<List<ChatMessage>>(this.filePath) ?? [];
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId)
    {
        var all = await this.GetAllAsync();
        return [.. all.Where(m => m.ChatRoomId == chatRoomId)];
    }

    public async Task AddAsync(ChatMessage message)
    {
        await this.semaphore.WaitAsync();
        try
        {
            var messages = await this.LoadJsonFileAsync<List<ChatMessage>>(this.filePath) ?? [];
            messages.Add(message);
            await SaveJsonFileAsync(this.filePath, messages);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        await this.semaphore.WaitAsync();
        try
        {
            await SaveJsonFileAsync(this.filePath, Array.Empty<ChatMessage>());
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task ClearMessagesByChatAsync(string chatRoomId)
    {
        await this.semaphore.WaitAsync();
        try
        {
            var messages = await this.LoadJsonFileAsync<List<ChatMessage>>(this.filePath) ?? [];
            var remaining = messages.Where(m => m.ChatRoomId != chatRoomId).ToList();
            await SaveJsonFileAsync(this.filePath, remaining);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<List<ChatRoom>> GetChatRoomsAsync()
    {
        await this.semaphore.WaitAsync();
        try
        {
            return await this.LoadJsonFileAsync<List<ChatRoom>>(this.chatRoomsFilePath) ?? [];
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task AddChatRoomAsync(ChatRoom chatRoom)
    {
        await this.semaphore.WaitAsync();
        try
        {
            var rooms = await this.LoadJsonFileAsync<List<ChatRoom>>(this.chatRoomsFilePath) ?? [];
            rooms.Add(chatRoom);
            await SaveJsonFileAsync(this.chatRoomsFilePath, rooms);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task RemoveChatRoomAsync(string chatRoomId)
    {
        await this.semaphore.WaitAsync();
        try
        {
            var rooms = await this.LoadJsonFileAsync<List<ChatRoom>>(this.chatRoomsFilePath) ?? [];
            var remaining = rooms.Where(c => c.Id != chatRoomId).ToList();
            await SaveJsonFileAsync(this.chatRoomsFilePath, remaining);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task<List<ChatParticipant>> GetChatParticipantsAsync()
    {
        await this.semaphore.WaitAsync();
        try
        {
            return await this.LoadJsonFileAsync<List<ChatParticipant>>(
                    this.chatParticipantsFilePath
                ) ?? [];
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task AddChatParticipantAsync(ChatParticipant participant)
    {
        await this.semaphore.WaitAsync();
        try
        {
            var participants =
                await this.LoadJsonFileAsync<List<ChatParticipant>>(this.chatParticipantsFilePath)
                ?? [];
            participants.Add(participant);
            await SaveJsonFileAsync(this.chatParticipantsFilePath, participants);
        }
        finally
        {
            this.semaphore.Release();
        }
    }

    public async Task RemoveChatParticipantAsync(string chatRoomId, string userId)
    {
        await this.semaphore.WaitAsync();
        try
        {
            var participants =
                await this.LoadJsonFileAsync<List<ChatParticipant>>(this.chatParticipantsFilePath)
                ?? [];
            var remaining = participants
                .Where(p => p.ChatRoomId != chatRoomId || p.UserId != userId)
                .ToList();
            await SaveJsonFileAsync(this.chatParticipantsFilePath, remaining);
        }
        finally
        {
            this.semaphore.Release();
        }
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
        this.semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
