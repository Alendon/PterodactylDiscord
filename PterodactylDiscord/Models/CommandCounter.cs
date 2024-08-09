using System.ComponentModel.DataAnnotations;

namespace PterodactylDiscord.Models;

public class CommandCounter
{
    [Key]
    public string Command { get; set; }
    
    public int Count { get; set; }
}