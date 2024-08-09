using Discord;
using Discord.Interactions;
using JetBrains.Annotations;

namespace PterodactylDiscord;

[UsedImplicitly]
public class DiscordCommands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Replies with pong")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong!");
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