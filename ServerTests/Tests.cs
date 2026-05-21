using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ServerPractice2026Spring.Model;

namespace ServerTests;

public class Tests
{
    private readonly HttpClient _httpClient = new HttpClient()
    {
        BaseAddress = new Uri("https://localhost:7125")
    };
    
    private readonly ChatDbContext _db = new();

    private HubConnection? _hub = null;
    
    [SetUp]
    public async Task Setup()
    {
        if (_hub is not null)
        {
            if (_hub.State == HubConnectionState.Connected)
                await _hub.StopAsync();

            await _hub.DisposeAsync();
            _hub = null;
        }
        _httpClient.DefaultRequestHeaders.Clear();
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.EnsureCreatedAsync();
    }

    [TestCase("admin1", "admin")]
    public async Task Registration(string login, string password)
    {
        var response = await _httpClient.GetFromJsonAsync<Response>($"api/Auth/Register?login={login}&password={password}");
        
        
        Assert.Multiple(() =>
        {
            Assert.That(response?.StatusCode, Is.EqualTo((HttpStatusCode)200));
            Assert.That(JsonConvert.DeserializeObject<bool>(response?.Data?.ToString() ?? ""), Is.True);
        });
    }

    [TestCase("admin123", "adm")]
    [TestCase("68549854", "admin")]
    [TestCase("adm", "adm")]
    public async Task RegistrationChecks(string login, string password)
    {
        var response = await _httpClient.GetFromJsonAsync<Response>($"api/Auth/Register?login={login}&password={password}");
        Assert.That(response?.StatusCode, Is.EqualTo((HttpStatusCode)400));
    }
    
    [Test]
    public async Task RegistrationNotAllowDoubleLogins()
    {
        var uri = "api/Auth/Register?login=admin&password=123456";
        var response1 = await _httpClient.GetFromJsonAsync<Response>(uri);
        var response2 = await _httpClient.GetFromJsonAsync<Response>(uri);

        Assert.Multiple(() =>
        {
            Assert.That(response1?.StatusCode == (HttpStatusCode)200 && JsonConvert.DeserializeObject<bool>(response1?.Data?.ToString() ?? ""));
            Assert.That(response2?.StatusCode, Is.EqualTo((HttpStatusCode)403));
        });
    }

    [Test]
    public async Task Authorization()
    {
        const string login = "admin1";
        const string password = "admin";
        await _httpClient.GetAsync($"api/Auth/Register?login={login}&password={password}");

        var response = await _httpClient.GetFromJsonAsync<Response>($"api/Auth/Authorize?login={login}&password={password}");
        var token = response?.Data?.ToString()?.Trim('"');
        
        Assert.Multiple(() =>
        {
            Assert.That(response?.StatusCode, Is.EqualTo((HttpStatusCode)200));
            Assert.That(new JwtSecurityTokenHandler().CanReadToken(token), Is.True);
        });
    }

    [TestCase("Somechat")]
    public async Task ChatCreation(string chatTitle, string[]? chatMembers = null, string login = "admin1", string password = "admin")
    {
        await Registration(login, password);
        var token = await GetToken(login, password);
        var hub = await CreateHub(token);

        Response? response = null;
        Chat? chat = null;
        try
        {
            response = await hub.InvokeAsync<Response>("CreateChat", "Somechat", chatMembers);
            chat = JsonConvert.DeserializeObject<Chat>(response.Data?.ToString() ?? "");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        Assert.Multiple(() =>
        {
            Assert.That(response?.StatusCode, Is.EqualTo((HttpStatusCode)201));
            Assert.That(chat, Is.Not.Null);
        });
    }

    private static IEnumerable<string[]> LoginsSource() => [["user1", "user2"]];
    
    [TestCaseSource(nameof(LoginsSource))]
    public async Task AddChatMembers(string[] logins)
    {
        await ChatCreation("Somechat");
        foreach (var login in logins)
        {
            await Registration(login, "password");
        }
        var hub = _hub!;
        var chat = JsonConvert.DeserializeObject<List<Chat>>((await hub.InvokeAsync<Response>("GetChats")).Data!.ToString()!)!.First();

        var response = await hub.InvokeAsync<Response>("AddChatMembers", chat!.Id, logins.ToList());
        
        
        Assert.That(response.StatusCode == ((HttpStatusCode)201) && JsonConvert.DeserializeObject<bool>(response?.Data?.ToString() ?? ""));
    }
    

    [OneTimeTearDown]
    public async Task Dispose()
    {
        if (_hub is not null)
        {
            if (_hub.State == HubConnectionState.Connected)
                await _hub.StopAsync();

            await _hub.DisposeAsync();
        }
        
        _httpClient.Dispose();
        await _db.DisposeAsync();
    }

    private async Task<string> GetToken(string login = "admin1", string password = "admin") => (await _httpClient.GetFromJsonAsync<Response>("api/Auth/Authorize?login=admin1&password=admin"))!.Data!.ToString()!.Trim('"');
    private async Task<HubConnection> CreateHub(string token)
    {
        _hub ??= new HubConnectionBuilder()
            .WithUrl("https://localhost:7125/chatHub", options => { options.Headers.Add("Authorization", "Bearer " + token); })
            .WithAutomaticReconnect()
            .Build();
        
        if (_hub.State != HubConnectionState.Connected)
            await _hub.StartAsync();
        
        return _hub;
    }

    private async Task<HubConnection> RegisterAndLogin(string login = "admin1", string password = "admin")
    {
        await Registration(login, password);
        return await CreateHub(await GetToken(login, password));
    }
}