using System.Collections;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using ServerPractice2026Spring.Model;
using ServerPractice2026Spring.Tools;

namespace ServerPractice2026Spring.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AuthController(ChatDbContext db, ILogger<AuthController> logger, UserIdsHandler idHandler) : ControllerBase
    {
        private readonly SecurityKey _publicKey = KeyHelper.BuildRsaSigningKey(Options.RSA);

        [HttpGet]
        [HttpPost]
        public async Task<Response> Login(string login, string password)
        {
            var passHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var passHashStr = Encoding.UTF8.GetString(passHash);
        
            var user = await db.Users.FirstOrDefaultAsync(u => u.Login == login 
                                                               && u.Password == passHashStr);

            if (user is null)
            {
                logger.LogInformation("{ConnectionId} tried to log in, but failed", HttpContext.Connection.Id);
                return ToBadResponse("Wrong pair login/password", 401);
            }
        
            var token = (object)GenerateToken(user.Login, DateTimeOffset.UtcNow.AddMinutes(20));
            logger.LogInformation("{ConnectionId} received authorize token", HttpContext.Connection.Id);
            return ToResponseWithData(token, "Authenticated successfully");
        }
        
        [HttpGet]
        [HttpPost]
        public async Task<Response> Register(string login, string password)
        {
            if (await db.Users.AnyAsync(u => u.Login == login))
            {
                logger.LogInformation("{ConnectionId} tried to register, but failed because of duplicate login", HttpContext.Connection.Id);
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
            logger.LogInformation("{ConnectionId} registered successfully under login: {login}", HttpContext.Connection.Id, login);
            return ToResponseWithData(true, "Registered successfully");
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
}
