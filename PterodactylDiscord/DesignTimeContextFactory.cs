using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PterodactylDiscord;

public class DesignTimeContextFactory: IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql(MariaDbServerVersion.LatestSupportedServerVersion);
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}