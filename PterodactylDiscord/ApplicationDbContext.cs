using Microsoft.EntityFrameworkCore;
using PterodactylDiscord.Models;

namespace PterodactylDiscord;

public class ApplicationDbContext : DbContext
{
    public DbSet<PterodactylServer> PterodactylServers { get; set; }
     
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
}