﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using JetBrains.Annotations;
using PterodactylDiscord.Services;

namespace PterodactylDiscord.DiscordCommands;

[PublicAPI]
public class ServerInteractionCommands(PterodactylService pterodactylService, ILogger<ServerInteractionCommands> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
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

        var serverRunningResponse = await pterodactylService.IsServerRunning(serverId);
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
            await FollowupAsync($"An error occurred while trying to restart the server: {error.Value}", ephemeral: true);
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
    
    private static HashSet<string> _refreshingServers = new();
    
    [ComponentInteraction("refresh:*")]
    public async Task RefreshServerInfo(string serverId)
    {
        await DeferAsync();
        await UpdateServerInfo(serverId);

        var id = Context.Interaction.Token;
        
        if (!_refreshingServers.Add(id)) return;
        
        logger.LogInformation("Starting background task to refresh server info for server {ServerId}, internal id {InternalId}", serverId, id);
        //queue a new background task that every minute will refresh the server info
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                if (await TryUpdateServerInfo(serverId)) continue;
                
                _refreshingServers.Remove(id);
                return;
            }
        });
    }
    
    private async Task<bool> TryUpdateServerInfo(string serverId)
    {
        try
        {
            var serverNameResponse = await pterodactylService.GetServerName(serverId);
            if (serverNameResponse.TryPickT1(out var error, out var serverName))
            {
                await FollowupAsync($"Error: {error.Value}", ephemeral: true);
                return false;
            }

            var serverRunningResponse = await pterodactylService.IsServerRunning(serverId);
            if (serverRunningResponse.TryPickT1(out error, out var serverRunning))
            {
                await FollowupAsync($"Error: {error.Value}", ephemeral: true);
                return false;
            }

            var embed = CreateServerEmbed(serverId, serverName, serverRunning);
            var components = CreateServerComponents(serverId, serverRunning);

            await ModifyOriginalResponseAsync(x =>
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

        var serverRunningResponse = await pterodactylService.IsServerRunning(serverId);
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
    }


    private static Embed CreateServerEmbed(string serverId, string serverName, bool serverRunning)
    {
        return new EmbedBuilder()
            .WithTitle(serverName)
            .WithDescription($"ID: {serverId}")
            .WithColor(serverRunning ? Color.Green : Color.Red)
            .AddField("Running: ", serverRunning ? "Yes" : "No")
            .WithFooter($"Last updated: {DateTime.UtcNow}")
            .Build();
    }

    private static MessageComponent CreateServerComponents(string serverId, bool serverRunning)
    {
        return new ComponentBuilder()
            .WithButton("Start", $"start:{serverId}", disabled: serverRunning)
            .WithButton("Stop", $"stop:{serverId}", disabled: !serverRunning)
            .WithButton("Restart", $"restart:{serverId}")
            .WithButton("Kill", $"kill:{serverId}", ButtonStyle.Danger, disabled: !serverRunning)
            .WithButton("Refresh", $"refresh:{serverId}")
            .Build();
    }


    
}