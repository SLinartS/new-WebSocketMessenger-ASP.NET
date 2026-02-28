
using System.Text.Json;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

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
        string dataDir =
            Environment.GetEnvironmentVariable("DATA_DIR")
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        filePath = Path.Combine(dataDir, "message_history.json");
        chatRoomsFilePath = Path.Combine(dataDir, "chat_rooms.json");
        chatParticipantsFilePath = Path.Combine(dataDir, "chat_participants.json");
    }

    private async Task<T?> LoadJsonFileAsync<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return default;
        }

        string json = await File.ReadAllTextAsync(filePath);
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
            logger.LogWarning(
                ex,
                "Corrupt JSON file {Path}, returning empty collection",
                filePath
            );
            return default;
        }
    }

    private static async Task SaveJsonFileAsync<T>(string filePath, T data)
    {
        string json = JsonSerializer.Serialize(data, jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<ChatMessage>> GetAllAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            return await LoadJsonFileAsync<List<ChatMessage>>(filePath) ?? [];
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<ChatMessage>> GetMessagesByChatAsync(string chatRoomId)
    {
        List<ChatMessage> all = await GetAllAsync();
        return [.. all.Where(m => m.ChatRoomId == chatRoomId)];
    }

    public async Task AddAsync(ChatMessage message)
    {
        await semaphore.WaitAsync();
        try
        {
            List<ChatMessage> messages = await LoadJsonFileAsync<List<ChatMessage>>(filePath) ?? [];
            messages.Add(message);
            await SaveJsonFileAsync(filePath, messages);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            await SaveJsonFileAsync(filePath, Array.Empty<ChatMessage>());
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task ClearMessagesByChatAsync(string chatRoomId)
    {
        await semaphore.WaitAsync();
        try
        {
            List<ChatMessage> messages = await LoadJsonFileAsync<List<ChatMessage>>(filePath) ?? [];
            var remaining = messages.Where(m => m.ChatRoomId != chatRoomId).ToList();
            await SaveJsonFileAsync(filePath, remaining);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<ChatRoom>> GetChatRoomsAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            return await LoadJsonFileAsync<List<ChatRoom>>(chatRoomsFilePath) ?? [];
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task AddChatRoomAsync(ChatRoom chatRoom)
    {
        await semaphore.WaitAsync();
        try
        {
            List<ChatRoom> rooms = await LoadJsonFileAsync<List<ChatRoom>>(chatRoomsFilePath) ?? [];
            rooms.Add(chatRoom);
            await SaveJsonFileAsync(chatRoomsFilePath, rooms);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task RemoveChatRoomAsync(string chatRoomId)
    {
        await semaphore.WaitAsync();
        try
        {
            List<ChatRoom> rooms = await LoadJsonFileAsync<List<ChatRoom>>(chatRoomsFilePath) ?? [];
            var remaining = rooms.Where(c => c.Id != chatRoomId).ToList();
            await SaveJsonFileAsync(chatRoomsFilePath, remaining);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<ChatParticipant>> GetChatParticipantsAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            return await LoadJsonFileAsync<List<ChatParticipant>>(
                    chatParticipantsFilePath
                ) ?? [];
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task AddChatParticipantAsync(ChatParticipant participant)
    {
        await semaphore.WaitAsync();
        try
        {
            List<ChatParticipant> participants =
                await LoadJsonFileAsync<List<ChatParticipant>>(chatParticipantsFilePath)
                ?? [];
            participants.Add(participant);
            await SaveJsonFileAsync(chatParticipantsFilePath, participants);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task RemoveChatParticipantAsync(string chatRoomId, string userId)
    {
        await semaphore.WaitAsync();
        try
        {
            List<ChatParticipant> participants =
                await LoadJsonFileAsync<List<ChatParticipant>>(chatParticipantsFilePath)
                ?? [];
            var remaining = participants
                .Where(p => p.ChatRoomId != chatRoomId || p.UserId != userId)
                .ToList();
            await SaveJsonFileAsync(chatParticipantsFilePath, remaining);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<ChatParticipant>> GetParticipantsByChatAsync(string chatRoomId)
    {
        List<ChatParticipant> all = await GetChatParticipantsAsync();
        return [.. all.Where(p => p.ChatRoomId == chatRoomId)];
    }

    public async Task<bool> IsUserInChatAsync(string chatRoomId, string userId)
    {
        List<ChatParticipant> participants = await GetParticipantsByChatAsync(chatRoomId);
        return participants.Any(p => p.UserId == userId);
    }

    public void Dispose()
    {
        semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
