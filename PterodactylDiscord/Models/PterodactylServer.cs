using System.ComponentModel.DataAnnotations;

namespace PterodactylDiscord.Models;

public class PterodactylServer
{
    [Key] public int Key { get; set; }

    [MaxLength(8)] public required string Identifier { get; set; }
    
    [MaxLength(64)]
    public required string Name { get; set; }
    
    /// <summary>
    /// How long to wait (in minutes) before shutting down the server when it's empty.
    /// </summary>
    public int ShutdownTimer { get; set; }
}