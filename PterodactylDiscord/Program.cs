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

builder.Services.AddHttpClient("Pterodactyl", client =>
{
    var baseUri = builder.Configuration["Pterodactyl:BaseUrl"] ??
                  throw new InvalidOperationException("Pterodactyl base URL is not set.");
    var apiKey = builder.Configuration["Pterodactyl:ApiKey"] ??
                 throw new InvalidOperationException("Pterodactyl API key is not set.");

    var uriBuilder = new UriBuilder(baseUri)
    {
        Path = "/api/client/"
    };

    client.BaseAddress = uriBuilder.Uri;

    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
});


builder.Services.AddSingleton(discordConfig);
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddSingleton<GameServerManager>();
builder.Services.AddSingleton<PterodactylService>();

builder.Services.AddHostedService(x => x.GetRequiredService<PterodactylService>());
builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await using var dbContext = await scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
    await dbContext.Database.MigrateAsync();
}

app.Run();