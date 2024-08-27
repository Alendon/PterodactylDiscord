using Discord;
using Discord.Interactions;
using JetBrains.Annotations;
using PterodactylDiscord.Models;
using PterodactylDiscord.Services;

namespace PterodactylDiscord.DiscordCommands;

[PublicAPI]
public class CommonCommands(PterodactylService pterodactylService, GameServerManager gameServerManager)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("track-server", "Add a server to track")]
    [RequireOwner]
    public async Task TrackServerAsync()
    {
        await RespondWithModalAsync(BuildServerModal("Track Server", "track-server"));
    }

    [SlashCommand("update-server", "Update a tracked server")]
    [RequireOwner]
    public async Task UpdateServerAsync([Autocomplete(typeof(ServerSelectAutocompleteHandler))] string serverId)
    {
        var serverResult = await pterodactylService.GetServer(serverId);
        
        if (serverResult.TryPickT1(out var serverError, out var server))
        {
            await RespondAsync($"Error: {serverError.Value}", ephemeral: true);
            return;
        }

        await RespondWithModalAsync(
            BuildServerModal("Update Server", "update-server", server));
    }

    private Modal BuildServerModal(string title, string modalId, PterodactylServer? server = null)
    {
        return new ModalBuilder(title, modalId)
            .AddTextInput("Server Identifier", "server-id", minLength: 8, maxLength: 8, required: true, value: server?.Identifier)
            .AddTextInput("Server Name", "server-name", minLength: 1, maxLength: 64, required: true, value: server?.Name)
            .AddTextInput("Shutdown Timer", "shutdown-timer", required: true, value: server?.ShutdownTimer.ToString())
            .AddTextInput("Min Received Bytes Per Minute", "min-received", required: true, value: server?.MinReceivedDelta.ToString())
            .AddTextInput("Min Sent Bytes Per Minute", "min-sent", required: true, value: server?.MinSentDelta.ToString())
            .Build();
    }

    [ModalInteraction("track-server")]
    public async Task ModalResponseAsync(TrackServerModal modal)
    {
        if (!int.TryParse(modal.ShutdownTimer, out var shutdownTimer))
        {
            await RespondAsync("Invalid shutdown timer", ephemeral: true);
            return;
        }

        if (!ulong.TryParse(modal.MinReceivedBytesPerMinute, out var minReceivedBytesPerMinute))
        {
            await RespondAsync("Invalid min received bytes per minute", ephemeral: true);
            return;
        }

        if (!ulong.TryParse(modal.MinSentBytesPerMinute, out var minSentBytesPerMinute))
        {
            await RespondAsync("Invalid min sent bytes per minute", ephemeral: true);
            return;
        }

        await DeferAsync(true);

        var result = await pterodactylService.AddServerToTrack(modal.ServerIdentifier, shutdownTimer, modal.ServerName,
            minReceivedBytesPerMinute, minSentBytesPerMinute);

        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Server added to track", ephemeral: true);
    }

    [ModalInteraction("update-server")]
    public async Task UpdateServerModalResponseAsync(TrackServerModal modal)
    {
        if (!int.TryParse(modal.ShutdownTimer, out var shutdownTimer))
        {
            await RespondAsync("Invalid shutdown timer", ephemeral: true);
            return;
        }
        
        if (!ulong.TryParse(modal.MinReceivedBytesPerMinute, out var minReceivedBytesPerMinute))
        {
            await RespondAsync("Invalid min received bytes per minute", ephemeral: true);
            return;
        }

        if (!ulong.TryParse(modal.MinSentBytesPerMinute, out var minSentBytesPerMinute))
        {
            await RespondAsync("Invalid min sent bytes per minute", ephemeral: true);
            return;
        }

        await DeferAsync(true);

        var result = await pterodactylService.UpdateServer(modal.ServerIdentifier, server =>
        {
            server.Name = modal.ServerName;
            server.ShutdownTimer = shutdownTimer;
            server.MinReceivedDelta = minReceivedBytesPerMinute;
            server.MinSentDelta = minSentBytesPerMinute;
        });
        
        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Server updated", ephemeral: true);
    }

    [SlashCommand("untrack-server", "Remove a server from track")]
    [RequireOwner]
    public async Task UntrackServerAsync([Autocomplete(typeof(ServerSelectAutocompleteHandler))] string serverId)
    {
        await DeferAsync(true);
        var result = await pterodactylService.RemoveServerFromTrack(serverId);
        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Server removed from track", ephemeral: true);
    }


    [SlashCommand("power-on", "Power on the physical server")]
    [RequireOwner]
    public async Task PowerOnAsync()
    {
        await DeferAsync(true);
        var result = await gameServerManager.EnsurePoweredUp();
        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Server is powered on", ephemeral: true);
    }

    [SlashCommand("enable-power-off", "Enable the power off feature")]
    [RequireOwner]
    public async Task EnablePowerOffAsync(bool enable)
    {
        gameServerManager.PowerOffEnabled = enable;
        await RespondAsync("Power off feature is now " + (enable ? "enabled" : "disabled"), ephemeral: true);
    }

    [SlashCommand("power-off", "Power off the physical server")]
    [RequireOwner]
    public async Task PowerOffAsync()
    {
        if (!gameServerManager.PowerOffEnabled)
        {
            await RespondAsync("Power off feature is disabled", ephemeral: true);
            return;
        }

        await DeferAsync(true);
        await gameServerManager.TriggerPowerOff();
        await FollowupAsync("Server is powering off", ephemeral: true);
    }


    [SlashCommand("ping", "Replies with pong")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong!");
    }
}

public class TrackServerModal : IModal
{
    public string Title => "Track a server";

    [InputLabel("Server Identifier")]
    [ModalTextInput("server-id", minLength: 8, maxLength: 8)]
    public string ServerIdentifier { get; set; } = string.Empty;

    [InputLabel("Server Name")]
    [ModalTextInput("server-name", minLength: 1, maxLength: 64)]
    public string ServerName { get; set; } = string.Empty;

    [InputLabel("Shutdown Timer")]
    [ModalTextInput("shutdown-timer")]
    public string ShutdownTimer { get; set; } = string.Empty;

    [InputLabel("Min Received Bytes Per Minute")]
    [ModalTextInput("min-received")]
    public string MinReceivedBytesPerMinute { get; set; } = string.Empty;

    [InputLabel("Min Sent Bytes Per Minute")]
    [ModalTextInput("min-sent")]
    public string MinSentBytesPerMinute { get; set; } = string.Empty;
}