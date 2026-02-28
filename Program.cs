using SimpleMessenger.Middleware;
using SimpleMessenger.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(builder.Configuration.GetValue("Server:Port", 5237)));

builder.Services.AddControllers();
builder.Services.AddSingleton<IClientManager, ClientManager>();
builder.Services.AddSingleton<IMessageRepository, JsonMessageRepository>();
builder.Services.AddSingleton<IChatService, ChatService>();

var app = builder.Build();

app.UseWebSockets();
app.UseStaticFiles();
app.UseMiddleware<WebSocketMiddleware>();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/index.html"));

Console.WriteLine($"Server: {string.Join(", ", app.Urls)}");
Console.WriteLine($"Static: {Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")}");
app.Run();
