using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace PterodactylDiscord.Services;

public class DiscordBotService(
    IConfiguration config,
    ILogger<DiscordBotService> logger,
    IServiceProvider services,
    DiscordSocketClient client,
    InteractionService interactionService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        client.Log += LogAsync;
        client.Ready += ReadyAsync;
        client.InteractionCreated += HandleInteraction;

        var token = config["DISCORD_TOKEN"];
        logger.LogError("Token is {Token}", token);
        
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Discord token is not set");
        }
        
        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
        
        await interactionService.AddModuleAsync<DiscordCommands>(services);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(client, interaction);
            await interactionService.ExecuteCommandAsync(context, services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling interaction");
        }
    }

    private async Task ReadyAsync()
    {
        logger.LogInformation("Bot is connected and ready to receive commands");
        await interactionService.RegisterCommandsGloballyAsync();
    }

    private Task LogAsync(LogMessage arg)
    {
        var level = arg.Severity switch
        {
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.None
        };

        logger.Log(level, arg.Message);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await client.DisposeAsync();
    }
}