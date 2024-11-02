using Discord;
using Discord.Interactions;
using Discord.Rest;
using JetBrains.Annotations;
using PterodactylDiscord.Models;
using PterodactylDiscord.Services;

namespace PterodactylDiscord.DiscordCommands;

[PublicAPI]
public class ServerInteractionCommands(PterodactylService pterodactylService, ILogger<ServerInteractionCommands> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    private static HashSet<DateTimeOffset> _refreshingServers = new();


    [RequireOwner]
    [SlashCommand("server-info", "Get information about a server")]
    public async Task GetServerInfo([Autocomplete(typeof(ServerSelectAutocompleteHandler))] string serverId)
    {
        await DeferAsync();

        var serverNameResponse = await pterodactylService.GetServerName(serverId);
        if (serverNameResponse.TryPickT1(out var error, out var serverName))
        {
            await FollowupAsync($"Error: {error.Value}", ephemeral: true);
            return;
        }

        var serverRunningResponse = await pterodactylService.GetServerState(serverId);
        if (serverRunningResponse.TryPickT1(out error, out var serverRunning))
        {
            await FollowupAsync($"Error: {error.Value}", ephemeral: true);
            return;
        }

        var embed = CreateServerEmbed(serverId, serverName, serverRunning);
        var components = CreateServerComponents(serverId, serverRunning);

        await FollowupAsync(embed: embed, components: components, ephemeral: true);
    }

    [ComponentInteraction("start:*")]
    public async Task StartServer(string serverId)
    {
        await DeferAsync();
        var response = await pterodactylService.StartServer(serverId, TimeSpan.FromMinutes(1));
        if (response.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"An error occurred while trying to start the server: {error.Value}", ephemeral: true);
            return;
        }

        await UpdateServerInfo(serverId);
    }

    [ComponentInteraction("stop:*")]
    public async Task StopServer(string serverId)
    {
        await DeferAsync();
        var response = await pterodactylService.StopServer(serverId, TimeSpan.FromMinutes(1));
        if (response.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"An error occurred while trying to stop the server: {error.Value}", ephemeral: true);
            return;
        }

        await UpdateServerInfo(serverId);
    }

    [ComponentInteraction("restart:*")]
    public async Task RestartServer(string serverId)
    {
        await DeferAsync();
        var response = await pterodactylService.RestartServer(serverId, TimeSpan.FromMinutes(1));
        if (response.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"An error occurred while trying to restart the server: {error.Value}",
                ephemeral: true);
            return;
        }

        await UpdateServerInfo(serverId);
    }

    [ComponentInteraction("kill:*")]
    public async Task KillServer(string serverId)
    {
        await DeferAsync();
        var response = await pterodactylService.KillServer(serverId, TimeSpan.FromMinutes(1));
        if (response.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"An error occurred while trying to kill the server: {error.Value}", ephemeral: true);
            return;
        }

        await UpdateServerInfo(serverId);
    }


    [ComponentInteraction("refresh:*")]
    public async Task RefreshServerInfo(string serverId)
    {
        await DeferAsync();
        await UpdateServerInfo(serverId);
    }

    private async Task TriggerAutoRefresh(string serverId)
    {
        var id = (await Context.Interaction.GetOriginalResponseAsync()).CreatedAt;

        lock (_refreshingServers)
            if (!_refreshingServers.Add(id))
                return;

        logger.LogInformation(
            "Starting background task to refresh server info for server {ServerId}, internal id {InternalId}", serverId,
            id);
        //queue a new background task that every minute will refresh the server info
        _ = Task.Run(async () =>
        {
            var originalResponse = await Context.Interaction.GetOriginalResponseAsync();
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                
                if (await TryUpdateServerInfo(serverId, originalResponse)) continue;

                lock (_refreshingServers)
                    _refreshingServers.Remove(id);
                return;
            }
        });
    }

    private async Task<bool> TryUpdateServerInfo(string serverId, RestInteractionMessage originalResponse)
    {
        try
        {
            var serverNameResponse = await pterodactylService.GetServerName(serverId);
            if (serverNameResponse.TryPickT1(out var error, out var serverName))
            {
                await FollowupAsync($"Error: {error.Value}", ephemeral: true);
                return false;
            }

            var serverRunningResponse = await pterodactylService.GetServerState(serverId);
            if (serverRunningResponse.TryPickT1(out error, out var serverRunning))
            {
                await FollowupAsync($"Error: {error.Value}", ephemeral: true);
                return false;
            }

            var embed = CreateServerEmbed(serverId, serverName, serverRunning);
            var components = CreateServerComponents(serverId, serverRunning);
            
            await originalResponse.ModifyAsync(x =>
            {
                x.Embed = embed;
                x.Components = components;
            });
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while trying to update server info");
            return false;
        }
    }

    private async Task UpdateServerInfo(string serverId)
    {
        var serverNameResponse = await pterodactylService.GetServerName(serverId);
        if (serverNameResponse.TryPickT1(out var error, out var serverName))
        {
            await FollowupAsync($"Error: {error.Value}", ephemeral: true);
            return;
        }

        var serverRunningResponse = await pterodactylService.GetServerState(serverId);
        if (serverRunningResponse.TryPickT1(out error, out var serverRunning))
        {
            await FollowupAsync($"Error: {error.Value}", ephemeral: true);
            return;
        }

        var embed = CreateServerEmbed(serverId, serverName, serverRunning);
        var components = CreateServerComponents(serverId, serverRunning);
        
        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed;
            x.Components = components;
        });

        await TriggerAutoRefresh(serverId);
    }


    private static Embed CreateServerEmbed(string serverId, string serverName, ServerState serverState)
    {
        return new EmbedBuilder()
            .WithTitle(serverName)
            .WithDescription($"ID: {serverId}")
            .WithColor(serverState switch
            {
                ServerState.Offline => Color.Red,
                ServerState.Starting => Color.Teal,
                ServerState.Running => Color.Green,
                ServerState.Stopping => Color.Orange,
                _ => Color.Purple
            })
            .AddField("State: ", serverState switch
            {
                ServerState.Offline => "Offline",
                ServerState.Starting => "Starting",
                ServerState.Running => "Running",
                ServerState.Stopping => "Stopping",
                _ => "Unknown"
            })
            .WithFooter($"Last updated: {DateTime.UtcNow}")
            .Build();
    }

    private static MessageComponent CreateServerComponents(string serverId, ServerState state)
    {
        return new ComponentBuilder()
            .WithButton("Start", $"start:{serverId}", disabled: state is not ServerState.Offline)
            .WithButton("Stop", $"stop:{serverId}", disabled: state is not ServerState.Running)
            .WithButton("Restart", $"restart:{serverId}")
            .WithButton("Kill", $"kill:{serverId}", ButtonStyle.Danger, disabled: state is ServerState.Offline)
            .WithButton("Refresh", $"refresh:{serverId}")
            .Build();
    }
}