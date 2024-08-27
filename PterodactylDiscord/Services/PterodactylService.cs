using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using OneOf;
using OneOf.Types;
using PterodactylDiscord.Models;

namespace PterodactylDiscord.Services;

public class PterodactylService(
    ILogger<PterodactylService> logger,
    IHttpClientFactory httpClientFactory,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    GameServerManager gameServerManager) : BackgroundService
{
    private readonly ConcurrentDictionary<string, LastServerState> _gameServerServices = new();

    public async Task<Dictionary<string, string>> GetTrackedServers()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.PterodactylServers.ToDictionaryAsync(s => s.Identifier, s => s.Name);
    }
    
    public async Task<OneOf<PterodactylServer, Error<string>>> GetServer(string serverIdentifier)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var server = await dbContext.PterodactylServers.FirstOrDefaultAsync(s => s.Identifier == serverIdentifier);

        if (server is null)
        {
            return new Error<string>("Server not found");
        }

        return server;
    }

    public async Task<OneOf<string, Error<string>>> GetServerName(string serverIdentifier)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var server = await dbContext.PterodactylServers.FirstOrDefaultAsync(s => s.Identifier == serverIdentifier);

        if (server is null)
        {
            return new Error<string>("Server not found");
        }

        return server.Name;
    }

    public async Task<OneOf<Success, Error<string>>> AddServerToTrack(string serverIdentifier, int shutdownTimer,
        string name, ulong minReceivedDelta, ulong minSentDelta)
    {
        if (!_gameServerServices.TryAdd(serverIdentifier, new LastServerState()))
        {
            return new Error<string>("Server already tracked");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        //check if a server with the same id or name already exists
        if (await dbContext.PterodactylServers.AnyAsync(s => s.Identifier == serverIdentifier))
        {
            return new Error<string>("Server already exists in database");
        }

        if (await dbContext.PterodactylServers.AnyAsync(s => s.Name == name))
        {
            return new Error<string>("Server name already exists in database");
        }

        await dbContext.PterodactylServers.AddAsync(new PterodactylServer()
        {
            Identifier = serverIdentifier,
            Name = name,
            ShutdownTimer = shutdownTimer,
            MinReceivedDelta = minReceivedDelta,
            MinSentDelta = minSentDelta
        });
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Added server {ServerIdentifier} to track", serverIdentifier);
        return new Success();
    }

    public async Task<OneOf<Success, Error<string>>> RemoveServerFromTrack(string serverIdentifier)
    {
        if (!_gameServerServices.Remove(serverIdentifier, out _))
        {
            return new Error<string>("Server not tracked");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var server = await dbContext.PterodactylServers.FirstOrDefaultAsync(s => s.Identifier == serverIdentifier);

        if (server is null)
        {
            return new Error<string>("Server not found in database");
        }

        dbContext.PterodactylServers.Remove(server);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Removed server {ServerIdentifier} from track", serverIdentifier);
        return new Success();
    }

    public async Task<OneOf<Success, Error<string>>> UpdateServer(string serverIdentifier, Action<PterodactylServer> update)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var server = await dbContext.PterodactylServers.FirstOrDefaultAsync(s => s.Identifier == serverIdentifier);

        if (server is null)
        {
            return new Error<string>("Server not found");
        }

        update(server);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Updated server {ServerIdentifier}", serverIdentifier);
        return new Success();
    }

    public async Task<OneOf<bool, Error<string>>> IsServerRunning(string serverIdentifier)
    {
        if (!await gameServerManager.IsPoweredOn()) return false;

        using var httpClient = CreateClient();
        var result = await GetResources(serverIdentifier, httpClient);

        if (result.TryPickT0(out var value, out var error))
        {
            return value.Attributes.CurrentState == ServerState.Running;
        }

        return error;
    }

    private async Task<OneOf<ResourceResponse, Error<string>>> GetResources(string serverIdentifier,
        HttpClient httpClient)
    {
        if (!_gameServerServices.ContainsKey(serverIdentifier))
        {
            return new Error<string>("Server not tracked");
        }

        if (!await gameServerManager.IsPoweredOn()) return new Error<string>("Server is not powered on");

        var httpResponse = await httpClient.GetAsync($"servers/{serverIdentifier}/resources");

        if (!httpResponse.IsSuccessStatusCode)
        {
            logger.LogError("Failed to get server resources {StatusCode}", httpResponse.StatusCode);
            return new Error<string>("Failed to get server resources");
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<ResourceResponse>();

        if (response is null)
        {
            return new Error<string>("Failed to parse server resources");
        }

        return response;
    }

    public async Task<OneOf<Success, Error<string>>> StartServer(string serverIdentifier, TimeSpan timeout)
    {
        _gameServerServices[serverIdentifier] = new LastServerState();
        
        var result = await gameServerManager.EnsurePoweredUp();
        if (result.TryPickT1(out var error, out _)) return error;

        return await SendPowerSignal(serverIdentifier, PowerSignal.Start, timeout);
    }

    public async Task<OneOf<Success, Error<string>>> StopServer(string serverIdentifier, TimeSpan timeout)
    {
        if (!await gameServerManager.IsPoweredOn()) return new Error<string>("Physical server is not powered on");

        return await SendPowerSignal(serverIdentifier, PowerSignal.Stop, timeout);
    }

    public async Task<OneOf<Success, Error<string>>> RestartServer(string serverIdentifier, TimeSpan timeout)
    {
        var result = await gameServerManager.EnsurePoweredUp();
        if (result.TryPickT1(out var error, out _)) return error;

        return await SendPowerSignal(serverIdentifier, PowerSignal.Restart, timeout);
    }

    public async Task<OneOf<Success, Error<string>>> KillServer(string serverIdentifier, TimeSpan timeout)
    {
        if (!await gameServerManager.IsPoweredOn()) return new Error<string>("Physical server is not powered on");

        return await SendPowerSignal(serverIdentifier, PowerSignal.Kill, timeout);
    }

    private async Task<OneOf<Success, Error<string>>> SendPowerSignal(string serverIdentifier, PowerSignal powerSignal,
        TimeSpan timeout)
    {
        var signal = powerSignal switch
        {
            PowerSignal.Start => "start",
            PowerSignal.Stop => "stop",
            PowerSignal.Restart => "restart",
            PowerSignal.Kill => "kill",
            _ => throw new ArgumentOutOfRangeException(nameof(powerSignal), powerSignal, null)
        };
        string body = $"{{\"signal\": \"{signal}\"}}";

        var sleepTime = TimeSpan.FromSeconds(5);
        var start = DateTime.UtcNow;
        logger.LogInformation("Sending power signal {ServerIdentifier} {Signal}", serverIdentifier, signal);

        do
        {
            var result = await SendPowerSignalInternal(serverIdentifier, body);
            if (!result.TryPickT1(out _, out var remainder))
            {
                return remainder;
            }

            await Task.Delay(sleepTime);
        } while (DateTime.UtcNow - start < timeout);

        return new Error<string>("Timeout");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<OneOf<Success, Retry, Error<String>>> SendPowerSignalInternal(string serverIdentifier,
        string payload)
    {
        using var httpClient = CreateClient();

        using var httpResponse = await httpClient.PostAsync($"servers/{serverIdentifier}/power",
            new StringContent(payload, new MediaTypeHeaderValue("application/json")));

        logger.LogInformation("Received response {StatusCode}", httpResponse.StatusCode);

        //The api returns 200 when the wing is not available. This is likely when the server is currently starting
        if (httpResponse.StatusCode == HttpStatusCode.OK)
        {
            return new Retry();
        }

        if (httpResponse.StatusCode == HttpStatusCode.NoContent)
        {
            return new Success();
        }

        logger.LogError("Failed to set power state {StatusCode}", httpResponse.StatusCode);
        return new Error<string>("Failed to set power state");
    }

    private struct Retry;

    [MustDisposeResource]
    private HttpClient CreateClient()
    {
        return httpClientFactory.CreateClient("Pterodactyl");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PopulateFromDb();

        logger.LogInformation("Pterodactyl service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            await UpdateServers();
        }

        logger.LogInformation("Pterodactyl service stopped");
        _gameServerServices.Clear();
    }

    private async Task PopulateFromDb()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var servers = await dbContext.PterodactylServers.ToListAsync();

        foreach (var server in servers)
        {
            _gameServerServices.TryAdd(server.Identifier, new LastServerState());
        }
    }

    private async Task UpdateServers()
    {
        //If the server is not powered on, we don't need to check the servers
        if (!await gameServerManager.IsPoweredOn()) return;

        using var httpClient = CreateClient();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        bool atLeastOneRunning = false;

        foreach (var (identifier, state) in _gameServerServices)
        {
            var server = await dbContext.PterodactylServers.FirstOrDefaultAsync(s => s.Identifier == identifier);
            if (server is null || server.ShutdownTimer < 0) continue;


            var result = await GetResources(identifier, httpClient);

            if (result.TryPickT1(out var error, out var resources))
            {
                logger.LogError("Failed to get resources for {ServerIdentifier}: {Error}", identifier, error.Value);
                continue;
            }

            if (resources.Attributes.CurrentState != ServerState.Running)
            {
                state.ReceivedBytes = 0;
                state.TransmittedBytes = 0;
                state.MinutesEmpty = 0;

                logger.LogInformation("Server {ServerIdentifier} is not running", identifier);
                continue;
            }

            var changed = resources.Attributes.Resources.NetworkReceivedBytes > state.ReceivedBytes + server.MinReceivedDelta;
            state.ReceivedBytes = resources.Attributes.Resources.NetworkReceivedBytes;

            changed |= resources.Attributes.Resources.NetworkTransmittedBytes > state.TransmittedBytes + server.MinSentDelta;
            state.TransmittedBytes = resources.Attributes.Resources.NetworkTransmittedBytes;

            if (changed)
            {
                state.MinutesEmpty = 0;
            }
            else
            {
                state.MinutesEmpty++;
            }

            if (state.MinutesEmpty >= server.ShutdownTimer)
            {
                logger.LogInformation("Server {ServerIdentifier} has been empty for {Minutes} minutes. Stopping",
                    identifier, server.ShutdownTimer);

                var stopResult = await StopServer(identifier, TimeSpan.Zero);
                if (stopResult.TryPickT1(out error, out _))
                {
                    logger.LogError("Failed to stop server {ServerIdentifier}: {Error}", identifier, error.Value);
                }

                continue;
            }

            atLeastOneRunning = true;

            logger.LogInformation("Server {ServerIdentifier} is running", identifier);
        }

        if (!atLeastOneRunning)
        {
            logger.LogInformation("No servers running. Triggering power off");
            await gameServerManager.TriggerPowerOff();
        }
    }


    record ResourceResponse
    {
        [JsonPropertyName("attributes")] public ResourceAttributes Attributes { get; set; } = null!;
    }

    class LastServerState
    {
        public ulong ReceivedBytes { get; set; }
        public ulong TransmittedBytes { get; set; }
        public int MinutesEmpty { get; set; }
    }
}

record ResourceAttributes
{
    [JsonPropertyName("current_state")] public string CurrentStateString { get; set; } = null!;

    [JsonPropertyName("resources")] public ResourcValue Resources { get; set; } = null!;

    public ServerState CurrentState =>
        CurrentStateString switch
        {
            "starting" => ServerState.Starting,
            "offline" => ServerState.Offline,
            "running" => ServerState.Running,
            "stopping" => ServerState.Stopping,
            _ => throw new InvalidOperationException("Invalid server state")
        };

    public record ResourcValue
    {
        [JsonPropertyName("network_rx_bytes")] public ulong NetworkReceivedBytes { get; set; }

        [JsonPropertyName("network_tx_bytes")] public ulong NetworkTransmittedBytes { get; set; }
    }
}

public enum PowerSignal
{
    Start,
    Stop,
    Restart,
    Kill
}