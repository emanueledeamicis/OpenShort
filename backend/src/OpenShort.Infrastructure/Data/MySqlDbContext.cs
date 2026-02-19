using Microsoft.EntityFrameworkCore;

namespace OpenShort.Infrastructure.Data;

public class MySqlDbContext : AppDbContext
{
    // Design-time constructor
    public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
    {
    }
}
