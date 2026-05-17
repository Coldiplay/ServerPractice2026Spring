using Microsoft.AspNetCore.SignalR;

namespace ServerPractic;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User.Claims.FirstOrDefault(c => c.Type == "ID")?.Value;
    }
}