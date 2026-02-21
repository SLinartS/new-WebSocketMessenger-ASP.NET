/*
 * WebSocketMiddleware.cs - Middleware для обработки WebSocket соединений
 * 
 * Аналог в PHP: это роут с обработкой WebSocket (например, в Swoole или Ratchet)
 * В ASP.NET middleware - это промежуточный слой между запросом и ответом
 */

using System.Net.WebSockets;
using SimpleMessenger.Services;

namespace SimpleMessenger.Middleware;

/*
 * Middleware класс
 * 
 * В ASP.NET каждый middleware имеет метод InvokeAsync(HttpContext context)
 * Аналог в PHP: public function process(ServerRequestInterface $request, RequestHandlerInterface $handler)
 */
public class WebSocketMiddleware
{
    // Следующий middleware в цепочке (аналог: $next($request) в PHP)
    private readonly RequestDelegate _next;
    
    // Сервис чата для обработки соединений
    private readonly IChatService _chatService;

    /*
     * Конструктор
     * .NET внедряет зависимости через конструктор автоматически
     */
    public WebSocketMiddleware(RequestDelegate next, IChatService chatService)
    {
        _next = next;
        _chatService = chatService;
    }

    /*
     * Главный метод - вызывается при каждом HTTP запросе
     * 
     * Аналог в PHP (Slim middleware):
     * public function __invoke($request, $response, $next)
     * {
     *     if ($request->getUri()->getPath() === '/ws') {
     *         // обрабатываем WebSocket
     *     }
     *     return $next($request, $response);
     * }
     */
    public async Task InvokeAsync(HttpContext context)
    {
        // Проверяем, что запрос к /ws
        // Аналог: if ($path === '/ws')
        if (context.Request.Path != "/ws")
        {
            // Передаём запрос следующему middleware (аналог: $next($request))
            await _next(context);
            return;
        }

        // Проверяем, что это WebSocket запрос
        // Аналог: if ($request->getHeaderLine('Upgrade') === 'websocket')
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Expected WebSocket request");
            return;
        }

        // Создаём токен отмены (аналог: pcntl_signal в PHP для graceful shutdown)
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        
        // Принимаем WebSocket соединение
        // Аналог: $server->upgrade() в Ratchet
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        
        // Генерируем уникальный ID клиента (первые 8 символов GUID)
        // Аналог: $clientId = bin2hex(random_bytes(8));
        var clientId = Guid.NewGuid().ToString()[..8];
        
        // Делегируем обработку ChatService
        // Аналог: (new ChatHandler($ws, $clientId))->handle();
        await _chatService.HandleClientAsync(clientId, ws, cts.Token);
    }
}
