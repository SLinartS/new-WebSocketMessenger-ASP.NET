using System.Text.Json;
using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public class JsonMessageRepository : IMessageRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonMessageRepository()
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(directory, "message_history.json");
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
}
