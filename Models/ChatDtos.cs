namespace SimpleMessenger.Models;

public class CreateChatRequest
{
    public string? UserId { get; set; }
    public string? TargetUserId { get; set; }
}

public class SwitchChatRequest
{
    public string? UserId { get; set; }
}

public class FindUserRequest
{
    public string? UserId { get; set; }
    public string? TargetUserId { get; set; }
}

public class UserSearchResult
{
    public bool Found { get; set; }
    public UserInfo? User { get; set; }
    public string? Message { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
}
