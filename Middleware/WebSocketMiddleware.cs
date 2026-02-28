using System.Net.WebSockets;
using SimpleMessenger.Services;

namespace SimpleMessenger.Middleware;

public class WebSocketMiddleware(RequestDelegate next, IChatService chatService)
{
    private readonly RequestDelegate next = next;
    private readonly IChatService chatService = chatService;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path != "/ws")
        {
            await next(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Expected WebSocket request");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);

        using WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();

        string? clientId = context.Request.Query["userId"].FirstOrDefault();
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = Guid.NewGuid().ToString()[..8];
        }

        string ipAddress = GetIpAddress(context.Connection.RemoteIpAddress);

        await chatService.HandleClientAsync(clientId, ws, ipAddress, cts.Token);
    }

    private static string GetIpAddress(System.Net.IPAddress? address)
    {
        if (address == null)
        {
            return "unknown";
        }

        string ipString = address.ToString();

        if (ipString.StartsWith("::ffff:"))
        {
            return ipString[7..];
        }

        return ipString;
    }
}
