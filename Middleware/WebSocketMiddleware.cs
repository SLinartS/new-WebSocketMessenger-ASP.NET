using System.Net.WebSockets;
using SimpleMessenger.Services;

namespace SimpleMessenger.Middleware;

public class WebSocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IChatService _chatService;

    public WebSocketMiddleware(RequestDelegate next, IChatService chatService)
    {
        _next = next;
        _chatService = chatService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path != "/ws")
        {
            await _next(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Expected WebSocket request");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        
        var clientId = context.Request.Query["userId"].FirstOrDefault();
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = Guid.NewGuid().ToString()[..8];
        }
        
        var ipAddress = GetIpAddress(context.Connection.RemoteIpAddress);
        
        await _chatService.HandleClientAsync(clientId, ws, ipAddress, cts.Token);
    }
    
    private static string GetIpAddress(System.Net.IPAddress? address)
    {
        if (address == null) return "unknown";
        
        var ipString = address.ToString();
        
        if (ipString.StartsWith("::ffff:"))
        {
            return ipString.Substring(7);
        }
        
        return ipString;
    }
}
