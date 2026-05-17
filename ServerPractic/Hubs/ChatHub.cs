using System.Collections;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bogus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using ServerPractic.Model;

namespace ServerPractic.Hubs;

[Authorize]
public class ChatHub(ChatDbContext db, Faker faker, UserIdsHandler idHandler) : Hub
{
    private readonly SecurityKey _publicKey = KeyHelper.BuildRsaSigningKey(Options.RSA);

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

        var userId = GetCurrentUserLogin();
        // Оно же по идее всегда true
        // if (await db.Users.FindAsync(userId) is null)
        //     return ToBadResponse("Unauthorized", 401);
        
        if (await db.ChatMembers.AnyAsync(cm => cm.ChatId == chatId 
                                                && cm.UserLogin == userId))
        {
            var message = await db.Messages.AddAsync(new Message()
            {
                Id = Guid.CreateVersion7(),
                Text = content.Length > 500 ? content.Remove(500, content.Length - 500) : content,
                SenderLogin = userId!,
                Timestamp = DateTimeOffset.UtcNow,
                ChatId = chatId
            });

            return ToResponseWithData(message);
        }
        return ToBadResponse("You are not a member of this chat", 403);
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

    public async Task<Response> Register(string login, string password)
    {
        if (await db.Users.AnyAsync(u => u.Login == login)) return ToBadResponse("Login already registered", 403);
        
        var passHash = SHA3_256.HashData(Encoding.UTF8.GetBytes(password));
        var passHashStr = Encoding.UTF8.GetString(passHash);
        var user = new User()
        {
            Login = login,
            Password = passHashStr,
        };
        await db.Users.AddAsync(user);
        return ToResponseWithData(true, "Registered successfully");
    }

    public async Task<Response> CreateChat(string chatTitle, List<string>? memberLogins = null)
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
        
        await Groups.AddToGroupAsync(userId.ToString(), chat.Id.ToString());
        
        
        if (memberLogins is not null && memberLogins.Count > 0)
        {
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
        }
        
        return ToResponseWithData(chat, "Chat created successfully", (HttpStatusCode)201);
    }


    private async Task SendMessagesToRandom(ulong chatId)
    {
        var chat = await db.ChatMembers.FirstOrDefaultAsync(c => c.ChatId == chatId);
        if (chat is null) return;

        var userLogins = faker.Random.Shuffle(db.ChatMembers.Where(c => c.ChatId == chatId).Select(c => c.UserLogin)).ToList();
        foreach (var userLogin in userLogins)
        {
            var userId = GetUserId(userLogin);
            if (userId is null) continue;
            try
            {
                var user = Clients.User(userId.ToString()!);
                if (user is not null)
                {
                    var messages = await db.Messages
                        .Where(m => m.ChatId == chatId)
                        .OrderByDescending(m => m.Timestamp)
                        .Take(faker.Random.Byte(20, 30))
                        .ToListAsync();
                    await user.SendAsync("GenerateResponse", messages);
                    break;
                }
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        

    }
    
    
    
    
    private string GenerateToken(string userLogin, DateTimeOffset expiry)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var guid = Guid.NewGuid();
        
        var identity = new ClaimsIdentity([
            new Claim("ID", guid.ToString())
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
        var id = Context.User?.Claims.FirstOrDefault(c => c.Type == "ID")?.Value;
        return Guid.TryParse(id, out var userGuidId) 
            ? idHandler.GetLogin(userGuidId) 
            : null;
    }

    private Guid? GetUserId(string login)
    {
        return idHandler.GetUserId(login);
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
}