using Discord;
using Discord.Interactions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using PterodactylDiscord.Models;

namespace PterodactylDiscord;

[UsedImplicitly]
public class DiscordCommands(IDbContextFactory<ApplicationDbContext> dbContextFactory) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Replies with pong")]
    public async Task PingAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var commandCounter = await dbContext.CommandCounters.FindAsync("ping");

        if (commandCounter is null)
        {
            commandCounter = new CommandCounter {Command = "ping", Count = 1};
            await dbContext.CommandCounters.AddAsync(commandCounter);
        }
        else
        {
            commandCounter.Count++;
        }
        
        await RespondAsync($"Pong! {commandCounter.Count}");
        
        await dbContext.SaveChangesAsync();
    }

    [RequireOwner]
    [SlashCommand("ping-slow", "Replies with pong")]
    public async Task PingSlowAsync()
    {
        await DeferAsync();
        await Task.Delay(3000);
        await FollowupAsync("This is a followup message");
        await Task.Delay(5000);
        await DeleteOriginalResponseAsync();
    }

    [SlashCommand("addition", "Adds two numbers")]
    public async Task AdditionAsync(
        [Summary(description: "The first number")]
        int first,
        [Summary(description: "The second number")]
        int second)
    {
        await RespondAsync($"The result is {first + second}");
    }
}