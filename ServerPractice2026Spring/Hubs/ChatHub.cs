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
public class ChatHub(ChatDbContext db, Faker faker, UserIdsHandler idHandler, ILogger<ChatHub> logger) : Hub
{
    private readonly SecurityKey _publicKey = KeyHelper.BuildRsaSigningKey(Options.RSA);
    private string ConnectionString => GetCurrentConnectionId().ToString() ?? $"Unauthorized connection ({Context.ConnectionId})";
    
    public async Task<Response> GetMessages(ulong chatId)
    {
        var login = GetCurrentUserLogin();
        if (!await db.ChatMembers.AnyAsync(c => c.ChatId == chatId
                                                && c.UserLogin == login))
        {
            logger.LogInformation("{ConnectionId} tried pulling messages from chat {chatId}, but is not member of it", ConnectionString, chatId);
            return ToBadResponse("You are not a member of this chat", 403);
        }
        
        var messages = await db.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Timestamp)
            .ToArrayAsync();
        logger.LogInformation("{ConnectionId} pulled {count} messages from chat {chatId}", ConnectionString, messages.Length, chatId);
        return ToResponseWithData(messages);
    }
    public async Task<Response> GetChats()
    {
        var login = GetCurrentUserLogin()!;
        var chats = await db.Chats.Include(c => c.Users)
            .Where(c => c.Users.Any(u => u.Login == login))
            .Select(c => new Chat() {
                Id = c.Id,
                Title = c.Title,
                Users = c.Users.Select(u => new User()
                {
                    Login = u.Login
                }).ToArray(),
            })
            .ToArrayAsync();

        logger.LogInformation("{ConnectionId} pulled {count} chats", ConnectionString, chats.Length);
        return ToResponseWithData(chats);
    }
    
    
    public async Task<Response> SendMessage(string content, ulong chatId)
    {
        content = content.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            logger.LogInformation("{ConnectionId} tried send message to chat {chatId} with empty ('{content}') content", ConnectionString, chatId, content);
            return ToBadResponse("Message text should not be empty", 400);
        }

        var userLogin = GetCurrentUserLogin()!;

        if (!await db.ChatMembers.AnyAsync(cm => cm.ChatId == chatId
                                                 && cm.UserLogin == userLogin))
        {
            logger.LogInformation("{ConnectionId} tried send message to chat {chatId}, but is not  member of it", ConnectionString, chatId);
            return ToBadResponse("You are not a member of this chat", 403);
        }
        
        
        var message = (await db.Messages.AddAsync(new Message()
        {
            Id = Guid.NewGuid(),
            Text = content.Length > 500 
                ? content.Remove(499, content.Length - 500) 
                : content,
            SenderLogin = userLogin,
            Timestamp = DateTimeOffset.UtcNow,
            ChatId = chatId
        })).Entity;
        await db.SaveChangesAsync();
        logger.LogInformation("{ConnectionId} sent message to chat {chatId}", ConnectionString, chatId);
        
        await Clients.OthersInGroup(chatId.ToString()).SendAsync("ReceiveMessage", message);
        return ToResponseWithData(message);
        
    }
    public async Task<Response> SendMessageAi(string content, ulong chatId)
    {
        var response = await SendMessage(content, chatId);
        if ((int)response.StatusCode < 400)
        {
            logger.LogInformation("{ConnectionId} sent ai message to chat {chatId}. Starting sending messages to random user...", ConnectionString, chatId);
            await SendMessagesToRandom(chatId, GetCurrentUserLogin());
        }
        else
        {
            logger.LogInformation("{ConnectionId} tried sending ai message to chat {chatId}, but failed because of '{message}' with statusCode: {statusCode}", ConnectionString, chatId, response.Message, response.StatusCode);
        }
        return response;
    }

    [AllowAnonymous]
    public async Task<Response> Authorize(string login, string password)
    {
        var passHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var passHashStr = Encoding.UTF8.GetString(passHash);
        
        var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login 
                                                           && u.Password == passHashStr);

        if (user is null)
        {
            logger.LogInformation("{ConnectionId} tried to log in, but failed", ConnectionString);
            return ToBadResponse("Wrong pair login/password", 401);
        }
        
        var token = GenerateToken(user.Login, DateTimeOffset.UtcNow.AddMinutes(20));
        logger.LogInformation("{ConnectionId} received authorize token", ConnectionString);
        return ToResponseWithData(token, "Authenticated successfully");
    }
    [AllowAnonymous]
    public async Task<Response> Register(string login, string password)
    {
        if (Options.LoginExpression.IsMatch(login) || Options.PasswordExpression.IsMatch(password))
        {
            logger.LogInformation("{ConnectionId} tried to register, but failed because of insufficient characters in login or password", ConnectionString);
            return ToBadResponse("Insufficient characters in login or password", 400);
        }
        if (await db.Users.AnyAsync(u => u.Login == login))
        {
            logger.LogInformation("{ConnectionId} tried to register, but failed because of duplicate login", ConnectionString);
            return ToBadResponse("Login already registered", 403);
        }
        
        var passHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var passHashStr = Encoding.UTF8.GetString(passHash);
        var user = new User()
        {
            Login = login,
            Password = passHashStr,
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        logger.LogInformation("{ConnectionId} registered successfully under login: {login}", ConnectionString, login);
        return ToResponseWithData(true, "Registered successfully");
    }

    
    public async Task<Response> CreateChat(string chatTitle, string[]? memberLogins = null)
    {
        chatTitle = chatTitle.Trim();
        if (string.IsNullOrWhiteSpace(chatTitle) || chatTitle.Length < 1)
        {
            logger.LogInformation("{ConnectionId} tried creating chat, but provided empty chat title ('{title}')", ConnectionString, chatTitle);
            return ToBadResponse("Chat title should be at least 1 character", 400);
        }
        var chat = new Chat()
        {
            Title = chatTitle
        };
        
        chat = (await db.Chats.AddAsync(chat)).Entity;
        await db.SaveChangesAsync();
        
        var userLogin = GetCurrentUserLogin()!;
        await db.ChatMembers.AddAsync(new ChatMember()
        {
            ChatId = chat.Id,
            UserLogin = userLogin
        });
        var connections = idHandler.GetConnectionIds(userLogin);

        if (connections is not null && connections.Count != 0)
        {
            foreach (var connection in connections)
            {
                await Groups.AddToGroupAsync(connection.ToString(), chat.Id.ToString());
            }
        }

        if (memberLogins is not null && memberLogins.Contains(userLogin))
        {
            memberLogins[memberLogins.IndexOf(userLogin)] = "";
        }
        
        if (memberLogins is null || memberLogins.Length == 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("{ConnectionId} created chat {chatId} with {count} member", ConnectionString, chat.Id, 1);
            return ToResponseWithData(chat, "Chat created successfully", (HttpStatusCode)201);
        }
        
        foreach (var memberLogin in memberLogins)
        {
            if (!await db.Users.AnyAsync(u => u.Login == memberLogin))
                continue;
            
            await db.ChatMembers.AddAsync(new ChatMember()
            {
                ChatId = chat.Id,
                UserLogin = memberLogin
            });
            var memberConnections = idHandler.GetConnectionIds(memberLogin);
            
            if (memberConnections is null || memberConnections.Count == 0) continue;
            foreach (var connection in memberConnections)
            {
                await Groups.AddToGroupAsync(connection.ToString(), chat.Id.ToString());
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("{ConnectionId} created chat {chatId} with {count} members", ConnectionString, chat.Id, memberLogins.Length + 1);
        return ToResponseWithData(chat, "Chat created successfully", (HttpStatusCode)201);
    }
    public async Task<Response> AddChatMembers(ulong chatId, List<string> logins)
    {
        if (logins.Count == 0)
        {
            logger.LogInformation("{ConnectionId} tried to add chat members to chat {chatId}, but provided empty logins", ConnectionString, chatId);
            return ToBadResponse("List of members is empty", 400);
        }

        var userLogin = GetCurrentUserLogin();
        if (!await db.ChatMembers.AnyAsync(c => c.ChatId == chatId
                                                && c.UserLogin == userLogin))
        {
            logger.LogInformation("{ConnectionId} tried to add chat members, while not being themselves a member of chat {chatId}", ConnectionString, chatId);
            return ToBadResponse("You are not a member of this chat", 403);
        }
        
        var loginsToRemove = new List<string>();
        for (int i = 0; i < logins.Count; i++)
        {
            if (!await db.Users.AnyAsync(u => u.Login == logins[i])
                && await db.ChatMembers.AnyAsync(c => c.UserLogin == logins[i] && c.ChatId == chatId))
            {
                loginsToRemove.Add(logins[i]);
                continue;
            }
            
            await db.ChatMembers.AddAsync(new ChatMember()
            {
                ChatId = chatId,
                UserLogin = logins[i]
            });
        }

        logins = logins.Except(loginsToRemove).ToList();

        if (logins.Count == 0)
        {
            logger.LogInformation("{ConnectionId} tried to add chat members to chat {chatId}, but provided unexistent logins", ConnectionString, chatId);
            return ToBadResponse("List of logins does not contain sufficient logins", 400);
        }
        
        await db.SaveChangesAsync();
        await AddRangeToGroup(logins.ToArray(), chatId.ToString());

        logger.LogInformation("{ConnectionId} added {count} members to chat {chatId}",  ConnectionString, logins.Count, chatId);
        return ToResponseWithData(true, "Successfully added members to chat", (HttpStatusCode)201);
    }
    
    
    private async Task SendMessagesToRandom(ulong chatId, string? removeLogin = null)
    {
        var userLogins = faker.Random.Shuffle(await 
            db.ChatMembers
                .Where(c => c.ChatId == chatId)
                .Select(c => c.UserLogin)
                .ToArrayAsync()).ToList();
        logger.LogInformation("Starting sending messages to random member of chat {chatId} (members: {count})", chatId, userLogins.Count);

        if (!string.IsNullOrWhiteSpace(removeLogin))
        {
            userLogins.Remove(removeLogin);
            logger.LogInformation("Removed login of sender");
        }
        
        foreach (var userLogin in userLogins)
        {
            var userIds = idHandler.GetConnectionIds(userLogin);
            if (userIds is null) continue;
            var userId = faker.PickRandom(userIds).ToString();
            
            var user = Clients.User(userId);
            
            var messages = await db.Messages
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.Timestamp)
                .Take(faker.Random.Byte(20, 30))
                .ToListAsync();
            logger.LogInformation("Sending {count} messages to {ConnectionId}", messages.Count, userId);
            await user.SendAsync("GenerateResponse", messages);
            logger.LogInformation("Sent {count} messages to {ConnectionId}", messages.Count, userId);
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
        logger.LogInformation("Starting adding {membersCount} to group {groupName}", logins.Length, groupName);
        foreach (var login in logins)
        {
            var connections = idHandler.GetConnectionIds(login);
            if (connections is null) continue;
            foreach (var connection in connections)
            {
                await Groups.AddToGroupAsync(connection.ToString(), groupName);
                logger.LogInformation("Connection {ConnectionId} added to group {groupName}", connection, groupName);
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
        var connectionId = GetCurrentConnectionId().ToString();
        if (!string.IsNullOrEmpty(connectionId))
        {
            var login = idHandler.GetLogin(Guid.Parse(connectionId!));
            var chatIdsWhereUserIn = await db.ChatMembers
                .Where(c => c.UserLogin == login)
                .Select(c => c.ChatId)
                .ToArrayAsync();

            logger.LogInformation("{ConnectionId} logged in. Starting to add them to {count} groups", connectionId, chatIdsWhereUserIn.Length);
            foreach (var chatId in chatIdsWhereUserIn)
            {
                await Groups.AddToGroupAsync(connectionId, chatId.ToString());
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