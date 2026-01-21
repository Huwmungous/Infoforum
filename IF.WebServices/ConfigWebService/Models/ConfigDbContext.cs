using Microsoft.EntityFrameworkCore;
using ConfigWebService.Entities;

namespace ConfigWebService.Data;

public class ConfigDbContext : DbContext
{
    public ConfigDbContext(DbContextOptions<ConfigDbContext> options)
        : base(options) { }

    public DbSet<ConfigEntry> ConfigEntries => Set<ConfigEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigEntry>()
            .HasKey(x => x.Idx);

        base.OnModelCreating(modelBuilder);
    }
}
