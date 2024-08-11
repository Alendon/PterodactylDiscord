using Discord.Interactions;
using JetBrains.Annotations;
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
        await RespondWithModalAsync<TrackServerModal>("track-server");
    }

    [ModalInteraction("track-server")]
    public async Task ModalResponseAsync(TrackServerModal modal)
    {
        if (!int.TryParse(modal.ShutdownTimer, out var shutdownTimer))
        {
            await RespondAsync("Invalid shutdown timer", ephemeral: true);
            return;
        }

        await DeferAsync(true);

        var result = await pterodactylService.AddServerToTrack(modal.ServerIdentifier, shutdownTimer, modal.ServerName);

        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Server added to track", ephemeral: true);
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

    [SlashCommand("set-server-name", "Set the name of a server")]
    [RequireOwner]
    public async Task SetServerNameAsync([Autocomplete(typeof(ServerSelectAutocompleteHandler))] string serverId,
        string serverName)
    {
        await DeferAsync(true);
        var result = await pterodactylService.UpdateServerName(serverId, serverName);
        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Server name set", ephemeral: true);
    }

    [SlashCommand("set-shutdown-timer", "Set the shutdown timer for a server")]
    [RequireOwner]
    public async Task SetShutdownTimerAsync([Autocomplete(typeof(ServerSelectAutocompleteHandler))] string serverId,
        int shutdownTimer)
    {
        await DeferAsync(true);
        var result = await pterodactylService.UpdateShutdownTimer(serverId, shutdownTimer);
        if (result.TryPickT1(out var error, out _))
        {
            await FollowupAsync($"Error: {error}", ephemeral: true);
        }

        await FollowupAsync("Shutdown timer set", ephemeral: true);
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
}