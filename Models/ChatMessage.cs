/*
 * ChatMessage.cs - Модель сообщения чата
 * 
 * Аналог в PHP: DTO (Data Transfer Object) или Entity класс
 * Это простой объект для передачи данных (аналог: array в PHP или Laravel Resource)
 */

using System.Text.Json.Serialization;

namespace SimpleMessenger.Models;

/*
 * Модель сообщения
 * 
 * В .NET используется Data Annotation [JsonPropertyName] для маппинга JSON ключей
 * Аналог в PHP: #[JsonSerializable] или просто массив с ключами
 * 
 * Пример JSON:
 * {
 *     "name": "John",
 *     "text": "Hello!",
 *     "type": "message"
 * }
 */
public class ChatMessage
{
    /*
     * Имя отправителя
     * 
     * [JsonPropertyName] указывает, какой JSON ключ маппить к этому свойству
     * Аналог в PHP: ключ массива 'name' => $message['name']
     */
    [JsonPropertyName("name")]
    public string? Name { get; set; } = "Anonymous";

    /*
     * Текст сообщения
     */
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /*
     * Тип сообщения: "message" (обычное) или "system" (системное)
     */
    [JsonPropertyName("type")]
    public string Type { get; set; } = "message";

    /*
     * Фабричный метод для создания обычного сообщения
     * 
     * Аналог в PHP:
     * public static function create(string $text, string $name): self
     * {
     *     return new self(['text' => $text, 'name' => $name, 'type' => 'message']);
     * }
     */
    public static ChatMessage Create(string text, string name) =>
        new() { Text = text, Name = name, Type = "message" };

    /*
     * Фабричный метод для создания системного сообщения
     * 
     * Аналог в PHP:
     * public static function system(string $text): self
     * {
     *     return new self(['text' => $text, 'type' => 'system']);
     * }
     */
    public static ChatMessage System(string text) =>
        new() { Text = text, Type = "system" };
}
