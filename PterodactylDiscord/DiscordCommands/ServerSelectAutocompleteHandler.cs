using Discord;
using Discord.Interactions;
using PterodactylDiscord.Services;

namespace PterodactylDiscord.DiscordCommands;

public class ServerSelectAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var currentInput = autocompleteInteraction.Data.Current.Value as string ?? "";
        var pterodactylService = services.GetRequiredService<PterodactylService>();

        var servers = await pterodactylService.GetTrackedServers();
        var suggestions = servers
            .Where(x => x.Value.Contains(currentInput, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult($"{x.Value}", x.Key));

        return AutocompletionResult.FromSuccess(suggestions);
    }
}