using Core.Interfaces;
using Infrastructure;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SeatRush.Middleware;
using SeatRush.SignalR;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<RedisUnavailableExceptionHandler>();
builder.Services.AddProblemDetails();

// Live seat map. The Redis backplane relays group messages between API instances, so a hold made on
// instance A reaches viewers connected to instance B. Same Redis as holds; channels are prefixed.
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!, o =>
    {
        o.Configuration.ChannelPrefix = RedisChannel.Literal("seatrush");
        o.Configuration.AbortOnConnectFail = false;
    });
builder.Services.AddSingleton<ISeatMapNotifier, SeatMapNotifier>();

// Guest identity: every user-specific endpoint needs a valid access token (signed JWT, 15 min).
// Validation is stateless: signature + issuer + audience + expiry, no database or Redis lookup per request.
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "sub" as "sub"
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = authOptions.Issuer,
            ValidAudience = authOptions.Audience,
            IssuerSigningKey = authOptions.CreateSigningKey(),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// The Angular app is served from a different origin (localhost:4200 locally, Vercel in production).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // needed for the refresh-token cookie and the SignalR client

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SeatMapHub>(SeatMapHub.Path);

// Apply migrations and seed the demo event on startup (single-instance deployment, so this is safe).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SeatRushContext>();
    await context.Database.MigrateAsync();
    await SeatRushContextSeed.SeedAsync(context);
}

app.Run();
