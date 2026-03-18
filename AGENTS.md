# AGENTS.md - Guide for Agentic Coding Assistants

This document provides essential information for AI agents working on the SimpleMessenger codebase.

## Project Overview

SimpleMessenger is a real-time WebSocket-based chat application built with ASP.NET Core 10.0. It supports multiple chat rooms, private messaging, user management, and typing indicators.

**Architecture:**
- Controllers: REST API endpoints (`Controllers/`)
- Services: Business logic and WebSocket handling (`Services/`)
- Middleware: WebSocket connection management (`Middleware/`)
- Models: Data transfer objects and entities (`Models/`)
- Frontend: Vanilla JS/HTML/CSS client (`wwwroot/`)

## Build, Lint, and Test Commands

### Build
```bash
dotnet build
dotnet build --configuration Release
```

### Run
```bash
dotnet run
# Server listens on port 5237 by default
```

### Format & Lint
```bash
# Format code
dotnet format SimpleMessenger.sln

# Verify formatting (pre-commit check)
dotnet format SimpleMessenger.sln --verify-no-changes --report --severity warn

# Format specific file
dotnet format Controllers/ChatController.cs
```

### Test
```bash
# Run all tests (if test project exists)
dotnet test

# Run single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run tests in specific file
dotnet test --filter "FullyQualifiedName~ClassName"
```

### Docker
```bash
# Build image
docker build -t simplemessenger .

# Run container
docker run -p 5237:5237 -v $(pwd)/data:/app/data simplemessenger
```

## Code Style Guidelines

### Imports and Using Directives
- **File-scoped namespaces**: Use `namespace SimpleMessenger.Services;` (not nested braces)
- **System directives first**: `dotnet_sort_system_directives_first = true`
- **Outside namespace**: Place using directives outside namespace declarations
- **No unused usings**: Enabled by analyzer (non-public parameters)

### Formatting (from .editorconfig)
- **Indentation**: 4 spaces (no tabs)
- **New lines**: Open brace on new line for all constructs (`csharp_new_line_before_open_brace = all`)
- **Spacing**: Spaces around binary operators, after keywords, after commas
- **Line endings**: LF on Unix files, CRLF on Windows
- **Final newline**: Required at end of every file

### Types and Variables
- **Explicit types**: Avoid `var` for built-in types (`csharp_style_var_for_built_in_types = false:suggestion`)
- **Keywords over BCL**: Use `string`, `int`, `bool` instead of `String`, `Int32`, `Boolean`
- **Nullable enabled**: All reference types are nullable by default (`<Nullable>enable</Nullable>`)
- **Read-only fields**: Prefer `readonly` for fields that don't change after construction

### Naming Conventions
- **Classes/Interfaces**: PascalCase (`ChatService`, `IChatService`)
- **Methods**: PascalCase (`GetMessagesAsync`, `SendMessage`)
- **Properties**: PascalCase (`ChatRoomId`, `IsTyping`)
- **Private/Internal fields**: camelCase (`clientManager`, `messageRepository`)
- **Static fields**: camelCase (prefix `s_` is optional, currently empty)
- **Constants**: PascalCase (`DefaultChatRoomId`)
- **Local variables**: camelCase (`clientId`, `chatRoomId`)
- **Parameters**: camelCase (`chatService`, `clientManager`)
- **Async methods**: Append `Async` suffix (`GetMessagesAsync`, `SendMessageAsync`)

### Error Handling
- **Argument validation**: Use `ArgumentNullException.ThrowIfNull()` or check `string.IsNullOrWhiteSpace()`
- **Logging**: Inject `ILogger<T>` and use structured logging with placeholders
  ```csharp
  logger.LogInformation("User {UserId} joined chat {ChatId}", userId, chatId);
  ```
- **Exception handling**: Log errors with context, rethrow or handle gracefully
  ```csharp
  catch (Exception ex)
  {
      logger.LogError(ex, "Error processing message for client {ClientId}", clientId);
      throw;
  }
  ```
- **Try-catch-finally**: Always release resources in `finally` blocks (e.g., `semaphore.Release()`)

### Method and Class Design
- **Primary constructors**: Use primary constructor syntax when possible (C# 12+)
  ```csharp
  public class ChatService(
      IClientManager clientManager,
      IMessageRepository messageRepository,
      ILogger<ChatService> logger
  )
  ```
- **Expression-bodied members**: Prefer for simple methods and properties
  ```csharp
  private static string SerializeMessage(object message) =>
      JsonSerializer.Serialize(message, jsonOptions);
  ```
- **Pattern matching**: Use switch expressions instead of nested if/else
  ```csharp
  await (message.Type switch
  {
      "findUser" => HandleFindUserAsync(clientId, message),
      "clear" => HandleClearAsync(message),
      _ => HandleChatMessageAsync(clientId, message),
  });
  ```
- **Object initializers**: Prefer object initializer syntax
  ```csharp
  var chatMessage = new ChatMessage
  {
      Text = message,
      Type = "message",
      ChatRoomId = chatRoomId,
  };
  ```

### Async/Await Patterns
- **Async all the way**: Don't use `.Result` or `.Wait()` (causes deadlocks)
- **ConfigureAwait(false)**: Not needed in ASP.NET Core (executes on thread pool)
- **CancellationToken**: Pass `CancellationToken` to async methods when available
- **Task return types**: Return `Task` for void async, `Task<T>` for value-returning async
- **Avoid async void**: Only use for event handlers

### Dependency Injection
- **Constructor injection**: Inject dependencies via constructor
- **Interface segregation**: Program to interfaces (`IChatService`, `IClientManager`)
- **Service lifetimes**: 
  - Singleton: `AddSingleton()` for thread-safe services (e.g., `ClientManager`, `JsonMessageRepository`)
  - Scoped: `AddScoped()` for request-scoped services (not currently used)
  - Transient: `AddTransient()` for lightweight services (not currently used)

### JSON Serialization
- **CamelCase property names**: Use `JsonNamingPolicy.CamelCase` for JSON output
- **JsonPropertyName**: Explicitly map properties with `[JsonPropertyName("id")]`
- **Static JsonSerializerOptions**: Reuse options instance to avoid allocation
  ```csharp
  private static readonly JsonSerializerOptions jsonOptions = new()
  {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };
  ```

### Security Considerations
- **Input sanitization**: Client-side HTML escaping only; store raw text
- **IP exposure**: Never include IP addresses in user-facing data
- **Authorization**: Verify user participation before chat operations
- **WebSocket validation**: Check `WebSocketState.Open` before sending
- **Error messages**: Avoid exposing sensitive information in errors

### Documentation
- **XML comments**: Add `<summary>` for public APIs (currently minimal)
- **Inline comments**: Explain complex logic or workarounds (see `ChatService.cs` step comments)
- **TODO comments**: Use `// TODO:` for temporary workarounds

### Git Hooks
- **Pre-commit**: Runs `dotnet format --verify-no-changes` automatically
- **Skip hooks**: Use `HUSKY=0` environment variable to disable (e.g., in Docker builds)

## File Structure Patterns

- **Controller classes**: One per area, use `[ApiController]` and `[Route]` attributes
- **Service interfaces**: Define contracts in `I*.cs` files
- **Service implementations**: Implement interfaces in `*.cs` files
- **Models**: Simple POCOs with init-only properties for immutability
- **Middleware**: Custom middleware classes following pipeline pattern

## Common Patterns in Codebase

### Repository Pattern (JsonMessageRepository)
- Thread-safe operations using `SemaphoreSlim`
- Load-modify-save pattern for JSON persistence
- Graceful handling of corrupt/missing files

### Event-driven Updates (ClientManager)
- Events for user state changes (`UsersChanged` event)
- Concurrency-safe with `ConcurrentDictionary`

### Message Broadcasting (ChatService)
- Enumerate clients and send to open connections
- Handle exceptions per-client, don't fail entire broadcast
- Support exclude sender option

### Switch Expression Dispatch
- Pattern matching on message types to route to handlers
- Each handler in its own private method for clarity

## Testing Notes

**Current state**: No test project exists in this codebase. When adding tests:

1. Create `SimpleMessenger.Tests` project
2. Add xUnit or NUnit dependencies
3. Use `WebApplicationFactory<T>` for integration tests
4. Mock services with Moq or NSubstitute for unit tests
5. Test WebSocket scenarios with `ClientWebSocket`

## Environment Variables

- `DATA_DIR`: Override default data directory path (default: `./data`)
- `HUSKY=0`: Disable git hooks (used in CI/Docker)

## Port Configuration

Server listens on port 5237 by default, configurable via `appsettings.json`:
```json
"Server": {
  "Port": 5237
}
```