using Microsoft.EntityFrameworkCore;
using PterodactylDiscord.Models;

namespace PterodactylDiscord;

public class ApplicationDbContext : DbContext
{
    public DbSet<CommandCounter> CommandCounters { get; set; }
    
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
}