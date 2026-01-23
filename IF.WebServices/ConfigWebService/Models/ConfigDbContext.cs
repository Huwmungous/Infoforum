using Microsoft.EntityFrameworkCore;
using ConfigWebService.Entities;

namespace ConfigWebService.Data;

public class ConfigDbContext(DbContextOptions<ConfigDbContext> options) : DbContext(options)
{
    public DbSet<ConfigEntry> ConfigEntries => Set<ConfigEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigEntry>(entity =>
        {
            entity.HasKey(x => x.Idx);

            // Unique constraint on realm + client
            entity.HasIndex(x => new { x.Realm, x.Client })
                .IsUnique()
                .HasDatabaseName("uq_usr_svc_settings_realm_client");

            // Configure jsonb columns
            entity.Property(x => x.UserConfig)
                .HasColumnType("jsonb");

            entity.Property(x => x.ServiceConfig)
                .HasColumnType("jsonb");

            entity.Property(x => x.BootstrapConfig)
                .HasColumnType("jsonb");
        });

        base.OnModelCreating(modelBuilder);
    }
}
