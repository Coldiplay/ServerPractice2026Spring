using Bogus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ServerPractice2026Spring.Hubs;
using ServerPractice2026Spring.MiddleWares;
using ServerPractice2026Spring.Model;
using ServerPractice2026Spring.Tools;
using SignalRSwaggerGen.Enums;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddDbContext<ChatDbContext>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Some API v1", Version = "v1" });
    options.AddSignalRSwaggerGen(c =>
    {
        c.IgnoreMethodsInheritedFromType(typeof(Hub));
        c.AutoDiscover = AutoDiscover.MethodsAndParams;
        c.HubMethodsScan = HubMethodsScan.Default;
    });

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    // options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    // {
    //     [new OpenApiSecuritySchemeReference("bearer", document)] = []
    // });
});

builder.Services.AddAuthentication(a => {
        a.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        a.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o => {
        var xml = Options.RSA;
        var key = KeyHelper.BuildRsaSigningKey(xml);

        o.RequireHttpsMetadata = false;
        o.SaveToken = true;
        o.IncludeErrorDetails = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience
        };
    });


builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
builder.Services.AddSingleton<UserIdsHandler>();
builder.Services.AddSingleton<Faker>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<GlobalExceptionMiddleWare>();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ChatHub>(Options.HubPath);
//app.MapControllers();

app.Run();