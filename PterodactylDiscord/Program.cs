using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using PterodactylDiscord;
using PterodactylDiscord.Services;

DiscordSocketConfig discordConfig = new()
{
    GatewayIntents = GatewayIntents.AllUnprivileged
};


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(discordConfig);
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddSingleton<GameServerManager>();

builder.Services.AddHostedService<DiscordBotService>();


var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();