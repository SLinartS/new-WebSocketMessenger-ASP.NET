using SimpleMessenger.Models;

namespace SimpleMessenger.Services;

public interface IMessageRepository
{
    Task<List<ChatMessage>> GetAllAsync();
    Task AddAsync(ChatMessage message);
}
