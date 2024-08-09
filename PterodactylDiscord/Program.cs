using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using PterodactylDiscord;
using PterodactylDiscord.Services;

DiscordSocketConfig discordConfig = new()
{
    GatewayIntents = GatewayIntents.AllUnprivileged
};


var builder = WebApplication.CreateBuilder(args);
Console.WriteLine(builder.Environment.EnvironmentName);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    {
        options.UseInMemoryDatabase("PterodactylDiscord");
    });
}
else
{
    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"Connection string: {connectionString}");
        
        var version = ServerVersion.AutoDetect(connectionString);
        options.UseMySql(connectionString, version);
    });
}

builder.Services.AddSingleton(discordConfig);
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddSingleton<GameServerManager>();

builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await using var dbContext = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/", () => "Hello World!");

app.Run();