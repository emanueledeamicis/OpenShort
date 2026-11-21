using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Data;

public class MySqlDbContext : AppDbContext
{
    // Design-time constructor
    public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The column was created as datetime(6) by the provider used until 0.3.1 (Pomelo).
        // The current provider would map it to datetime: keep the type of the existing databases.
        builder.Entity<AppUser>().Property(user => user.LockoutEnd).HasColumnType("datetime(6)");
    }
}
