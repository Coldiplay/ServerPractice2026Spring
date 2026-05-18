using System.Collections;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ServerPractic.Model;

namespace ServerPractic.Hubs;

[Authorize]
public class ChatHub(ChatDbContext db) : Hub
{

    public async Task<Response> GetMessages()
    {
        return ToResponseWithData(new Message());
    }


    public async Task<Response> SendMessage(string content, ulong chatId)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ToBadResponse("Message text should not be empty", 400);
        }

        var userId = GetUserId() ?? 0;
        if (await db.Users.FindAsync(userId) is null)
            return ToBadResponse("Unauthorized", 401);
        
        if ((await db.ChatMembers.FirstOrDefaultAsync(cm => cm.ChatId == chatId
                                                            && cm.UserId == userId))
                is not null)
        {
            var message = await db.Messages.AddAsync(new Message()
            {
                Id = Guid.CreateVersion7(),
                Text = content.Length > 500 ? content.Remove(500, content.Length - 500) : content,
                SenderId = userId,
                Timestamp = DateTimeOffset.UtcNow,
                ChatId = chatId
            });

            return ToResponseWithData(message);
        }
        return ToBadResponse("You are not a member of this chat", 403);
    }

    public async Task<Response> Authorize(string login, string password)
    {
        var passHash = SHA3_256.HashData(Encoding.UTF8.GetBytes(password));
        var passHashStr = Encoding.UTF8.GetString(passHash);
        
        
    }


    private Response ToResponseWithData<T>(T? data, string? message = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var returnData = JsonConvert.SerializeObject(data);
        if (string.IsNullOrEmpty(message))
        {
            var type = typeof(T);
            
            var typeName = type.IsGenericType
                ? type.GetGenericArguments()[0].Name
                : type.Name;
            var isCollection = typeof(IEnumerable).IsAssignableFrom(type);
            if (isCollection) typeName += "s";
            message = $"{typeName} retrieved successfully";
        }
        
        return new Response()
        {
            Data = returnData,
            Message = message,
            StatusCode = statusCode,
        };
    }

    private ulong? GetUserId()
    {
        var id = Context.User?.Claims.FirstOrDefault(c => c.Type == ClaimsIdentity.DefaultNameClaimType)?.Value;
        ulong.TryParse(id, out ulong userId);
        return userId;
    }

    private Response ToBadResponse(string message, ushort statusCode = 400)
        => ToBadResponse(message, (HttpStatusCode)statusCode);
    private Response ToBadResponse(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
        return new Response()
        {
            Message = message,
            StatusCode = statusCode
        };
    }
}