using Microsoft.AspNetCore.SignalR;

namespace ServerPractice2026Spring.Tools;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User.Claims.FirstOrDefault(c => c.Type == "Login")?.Value;
    }
}