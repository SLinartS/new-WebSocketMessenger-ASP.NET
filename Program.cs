using System.Text.Json;
using System.Text.Json.Serialization;
using SimpleMessenger.Middleware;
using SimpleMessenger.Services;

/*
 * Program.cs - Точка входа в приложение ASP.NET
 * 
 * Аналог в PHP: это как index.php или composer.json с autoload
 * В ASP.NET (.NET) приложение всегда начинается с Program.cs
 */

// В .NET используется паттерн "Builder" для создания приложения
// Это похоже на $app = new \Slim\App() в Slim Framework или $app = Laravel::make() в Laravel

var builder = WebApplication.CreateBuilder(args);

/*
 * Настройка Kestrel сервера (вместо IIS/Nginx)
 * 
 * Аналог в PHP: настройки Apache/Nginx
 * Kestrel - встроенный веб-сервер .NET (аналог: PHP-FPM)
 * 
 * 0.0.0.0 = слушать на всех сетевых интерфейсах (доступ из сети)
 */
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP - порт для подключения из сети
    options.ListenAnyIP(5237);
    
    // HTTPS - порт 7150 (можно отключить если не нужен)
    // options.ListenAnyIP(7150);
});

/*
 * Регистрация сервисов (Dependency Injection)
 * 
 * В PHP обычно используется PSR-11 контейнер или вручную создаёшь синглтоны
 * Здесь .NET имеет встроенный DI-контейнер, похожий на Laravel Service Container
 * 
 * AddSingleton - создаёт одну копию сервиса на всё время работы приложения
 * (аналог: $app->singleton() в Laravel)
 */
builder.Services.AddSingleton<IClientManager, ClientManager>();
builder.Services.AddSingleton<IChatService, ChatService>();

/*
 * Настройка JSON сериализации
 * 
 * .NET по умолчанию использует PascalCase для свойств (Name, Text)
 * Мы настраиваем camelCase (name, text) для совместимости с JavaScript
 * 
 * Аналог в PHP: json_encode($data, JSON_OBJECT_AS_ARRAY) или настройка Groups в Symfony
 */
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

/*
 * Создание приложения (build)
 * Аналог: $app = $builder->build(); в Laravel
 */
var app = builder.Build();

/*
 * Middleware (промежуточное ПО)
 * 
 * В PHP это аналог middleware в Laravel или Slim
 * Каждый middleware может модифицировать request/response
 */

// Поддержка WebSocket соединений
app.UseWebSockets();

// Раздача статических файлов (CSS, JS, HTML)
// Аналог: Route::get('/{path?}', ...)->where('path', '.*') в Laravel для public папки
app.UseStaticFiles();

// Подключаем наш WebSocket middleware
app.UseMiddleware<WebSocketMiddleware>();

/*
 * Маршрутизация (Routing)
 * 
 * GET / -> перенаправляет на /index.html
 * Аналог: Route::get('/', function() { return redirect('/index.html'); });
 */
app.MapGet("/", () => Results.Redirect("/index.html"));

// Запуск сервера
Console.WriteLine($"Server: {string.Join(", ", app.Urls)}");
Console.WriteLine($"Static: {Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")}");
app.Run();

