using System.Net.NetworkInformation;
using JetBrains.Annotations;
using MagicPacket;
using OneOf;
using OneOf.Types;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using ConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace PterodactylDiscord.Services;

public class GameServerManager(IConfiguration configuration, ILogger<GameServerManager> logger)
{
    public bool PowerOffEnabled { get; set; } = true;

    public async Task<OneOf<Success, Error<string>>> EnsurePoweredUp()
    {
        var serverIp = configuration["GameServer:Ip"] ?? throw new InvalidOperationException("GameServer:Ip not set");

        var ping = new Ping();
        var reply = await ping.SendPingAsync(serverIp);

        if (reply.Status == IPStatus.Success)
        {
            return new Success();
        }

        await TriggerPowerOn();
        var timeout = TimeSpan.FromMinutes(5);
        if(!await WaitForPowerOn(timeout))
            return new Error<string>("Failed to power on server");
        
        return new Success();
    }

    public async Task<bool> WaitForPowerOn(TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (await IsPoweredOn())
            {
                return true;
            }

            await Task.Delay(1000);
        }

        return false;
    }

    public async Task<bool> IsPoweredOn()
    {
        var serverIp = configuration["GameServer:Ip"] ?? throw new InvalidOperationException("GameServer:Ip not set");

        var ping = new Ping();
        var reply = await ping.SendPingAsync(serverIp);

        return reply.Status == IPStatus.Success;
    }

    private async Task TriggerPowerOn()
    {
        using var client = new MagicPacketClient();
        var address = configuration["GameServer:MacAddress"] ??
                      throw new InvalidOperationException("GameServer:MacAddress not set");
        if (!PhysicalAddress.TryParse(address, out var physicalAddress))
            throw new InvalidOperationException("Invalid MAC address");

        await client.BroadcastOnAllInterfacesAsync(physicalAddress);
        
        logger.LogInformation("Sent magic packet to power on server");
    }

    public async Task TriggerPowerOff()
    {
        if (!PowerOffEnabled) return;
        if (!await IsPoweredOn()) return;

        var shutdownCommand = configuration["GameServer:ShutdownCommand"] ??
                              throw new InvalidOperationException("GameServer:ShutdownCommand not set");
        
        using var client = BuildSshClient();

        await client.ConnectAsync(default);

        using var command = client.CreateCommand(shutdownCommand);

        try
        {
            await command.ExecuteAsync();
        }
        catch (SshConnectionException connectionException)
        {
            // If the connection was lost, the server was probably shut down
            if(connectionException.DisconnectReason != DisconnectReason.ConnectionLost) throw;
        }

        logger.LogInformation("Sent shutdown command to server");
    }

    [MustDisposeResource]
    private SshClient BuildSshClient()
    {
        var serverIp = configuration["GameServer:Ip"] ?? throw new InvalidOperationException("GameServer:Ip not set");
        var username = configuration["GameServer:Username"] ??
                       throw new InvalidOperationException("GameServer:Username not set");

        string? password = configuration["GameServer:Password"];
        string? privateKey = configuration["GameServer:PrivateKey"];

        AuthenticationMethod authenticationMethod = (privateKey, password) switch
        {
            ({ } key, _) => new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(key)),
            (_, { } pass) => new PasswordAuthenticationMethod(username, pass),
            _ => throw new InvalidOperationException(
                "No authentication method provided, GameServer:Password or GameServer:PrivateKey must be set")
        };

        var connectionInfo = new ConnectionInfo(serverIp, username, authenticationMethod);

        return new SshClient(connectionInfo);
    }
}