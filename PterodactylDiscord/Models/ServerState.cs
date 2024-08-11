using System.Text.Json.Serialization;

namespace PterodactylDiscord.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ServerState
{
    Starting,
    Offline,
    Running,
    Stopping
}