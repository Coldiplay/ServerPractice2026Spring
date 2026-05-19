using System.Collections;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bogus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using ServerPractice2026Spring.Model;
using ServerPractice2026Spring.Tools;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace ServerPractice2026Spring.Hubs;

[SignalRHub(Options.HubPath, AutoDiscover.MethodsAndParams, hubMethodsScan: HubMethodsScan.Default)]
[Authorize]
public class ChatHub(ChatDbContext db, Faker faker, UserIdsHandler idHandler) : Hub
{
    private readonly SecurityKey _publicKey = KeyHelper.BuildRsaSigningKey(Options.RSA);

    public async Task<Response> GetMessages(ulong chatId)
    {
        var login = GetCurrentUserLogin();
        if (!await db.ChatMembers.AnyAsync(c => c.ChatId == chatId
                                                && c.UserLogin == login)) 
            return ToBadResponse("You are not a member of this chat", 403);
        
        var messages = await db.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Timestamp)
            .ToArrayAsync();
        return ToResponseWithData(messages);
    }
    public async Task<Response> SendMessage(string content, ulong chatId)
    {
        if (string.IsNullOrEmpty(content))
        {
            return ToBadResponse("Message text should not be empty", 400);
        }

        var userId = GetCurrentUserLogin();

        if (!await db.ChatMembers.AnyAsync(cm => cm.ChatId == chatId
                                                 && cm.UserLogin == userId))
            return ToBadResponse("You are not a member of this chat", 403);
        
        
        var message = (await db.Messages.AddAsync(new Message()
        {
            Id = Guid.CreateVersion7(),
            Text = content.Length > 500 
                ? content.Remove(499, content.Length - 500) 
                : content,
            SenderLogin = userId!,
            Timestamp = DateTimeOffset.UtcNow,
            ChatId = chatId
        })).Entity;

        return ToResponseWithData(message);
    }

    public async Task<Response> SendMessageAi(string content, ulong chatId)
    {
        var response = await SendMessage(content, chatId);
        if ((int)response.StatusCode < 400)
            await SendMessagesToRandom(chatId);
        return response;
    }

    [AllowAnonymous]
    public async Task<Response> Authorize(string login, string password)
    {
        var passHash = SHA3_256.HashData(Encoding.UTF8.GetBytes(password));
        var passHashStr = Encoding.UTF8.GetString(passHash);
        
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login && u.Password == passHashStr);
        
        if (user is null) return ToBadResponse("Wrong pair login/password", 401);
        
        var token = GenerateToken(user.Login, DateTimeOffset.UtcNow.AddMinutes(20));
        return ToResponseWithData(token, "Authenticated successfully");
    }
    [AllowAnonymous]
    public async Task<Response> Register(string login, string password)
    {
        if (await db.Users.AnyAsync(u => u.Login == login)) 
            return ToBadResponse("Login already registered", 403);
        
        var passHash = SHA3_256.HashData(Encoding.UTF8.GetBytes(password));
        var passHashStr = Encoding.UTF8.GetString(passHash);
        var user = new User()
        {
            Login = login,
            Password = passHashStr,
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        return ToResponseWithData(true, "Registered successfully");
    }

    
    public async Task<Response> CreateChat(string chatTitle, string[]? memberLogins = null)
    {
        var chat = new Chat()
        {
            Title = chatTitle,
        };
        
        chat = (await db.Chats.AddAsync(chat)).Entity;

        var userId = GetCurrentUserLogin()!;
        await db.ChatMembers.AddAsync(new ChatMember()
        {
            ChatId = chat.Id,
            UserLogin = userId
        });
        
        await Groups.AddToGroupAsync(userId, chat.Id.ToString());


        if (memberLogins is null || memberLogins.Length <= 0)
        {
            await db.SaveChangesAsync();
            return ToResponseWithData(chat, "Chat created successfully", (HttpStatusCode)201);
        }
        
        foreach (var memberId in memberLogins)
        {
            await db.ChatMembers.AddAsync(new ChatMember()
            {
                ChatId = chat.Id,
                UserLogin = memberId
            });
            Clients.User(memberId);
            await Groups.AddToGroupAsync(memberId, chat.Id.ToString());
        }

        await db.SaveChangesAsync();

        return ToResponseWithData(chat, "Chat created successfully", (HttpStatusCode)201);
    }

    public async Task<Response> AddChatMembers(ulong chatId, string[] logins)
    {
        if (logins.Length == 0)
        {
            return ToBadResponse("List of members is empty", 400);
        }

        var userLogin = GetCurrentUserLogin();
        if (!await db.ChatMembers.AnyAsync(c => c.ChatId == chatId
                                                && c.UserLogin == userLogin))
            return ToBadResponse("You are not a member of this chat", 403);
        
        
        foreach (var login in logins)
        {
            await db.ChatMembers.AddAsync(new ChatMember()
            {
                ChatId = chatId,
                UserLogin = login
            });
        }

        await db.SaveChangesAsync();
        await AddRangeToGroup(logins, chatId.ToString());

        return ToBadResponse("You are not a member of this chat", 403);
    }

    
    
    private async Task SendMessagesToRandom(ulong chatId)
    {
        var userLogins = faker.Random.Shuffle(await 
            db.ChatMembers
                .Where(c => c.ChatId == chatId)
                .Select(c => c.UserLogin)
                .ToArrayAsync()).ToList();

        userLogins.Remove(GetCurrentUserLogin()!);
        
        foreach (var userLogin in userLogins)
        {
            var userIds = idHandler.GetConnectionIds(userLogin);
            if (userIds is null) continue;
            var userId = faker.PickRandom(userIds);
            
            var user = Clients.User(userId.ToString()!);
            
            var messages = await db.Messages
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.Timestamp)
                .Take(faker.Random.Byte(20, 30))
                .ToListAsync();
            await user.SendAsync("GenerateResponse", messages);
            break;
        }
    }
    
    private string GenerateToken(string userLogin, DateTimeOffset expiry)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var guid = Guid.NewGuid();
        
        var identity = new ClaimsIdentity([
            new Claim("GUID", guid.ToString())
        ]);
        idHandler.Add(userLogin, guid);

        var token = new JwtSecurityToken
        (
            Options.Issuer,
            Options.Audience,
            identity.Claims,
            DateTimeOffset.UtcNow.UtcDateTime,
            expiry.UtcDateTime,
            new SigningCredentials(_publicKey, SecurityAlgorithms.RsaSha256,
                SecurityAlgorithms.Sha256Digest)
        );
        var tokenString = tokenHandler.WriteToken(token);
        return tokenString;
    }
    private string? GetCurrentUserLogin()
    {
        return idHandler.GetLogin(GetCurrentConnectionId());
    }

    private Guid? GetCurrentConnectionId()
    {
        var id = Context.User?.Claims.FirstOrDefault(c => c.Type == "GUID")?.Value;
        return Guid.TryParse(id, out var guid)
            ? guid
            : null;
    }

    private async Task AddRangeToGroup(string[] logins, string groupName)
    {
        foreach (var login in logins)
        {
            var connections = idHandler.GetConnectionIds(login);
            if (connections is null) continue;
            foreach (var connection in connections)
            {
                await Groups.AddToGroupAsync(connection.ToString(), groupName);
            }
        }
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

    [ApiExplorerSettings(IgnoreApi = true)]
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.User?.Claims.FirstOrDefault(c => c.Type == "GUID")?.Value;
        if (string.IsNullOrEmpty(connectionId))
        {
            var login = idHandler.GetLogin(Guid.Parse(connectionId!));
            var chatIdsWhereUserIn = await db.ChatMembers
                .Where(c => c.UserLogin == login)
                .Select(c => c.ChatId)
                .ToArrayAsync();

            foreach (var chatId in chatIdsWhereUserIn)
            {
                await Groups.AddToGroupAsync(connectionId!, chatId.ToString());
            }    
        }
        
        await base.OnConnectedAsync();
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = GetCurrentConnectionId();
        idHandler.Remove(connectionId);
        
        await base.OnDisconnectedAsync(exception);
    }
}