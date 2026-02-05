using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Link> Links { get; set; }
    public DbSet<Domain> Domains { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Ensure Domain+Slug is unique
        modelBuilder.Entity<Link>()
            .HasIndex(l => new { l.Domain, l.Slug })
            .IsUnique();

        modelBuilder.Entity<Domain>()
            .HasIndex(d => d.Host)
            .IsUnique();
    }
}
