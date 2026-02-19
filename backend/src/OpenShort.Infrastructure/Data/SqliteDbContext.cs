using Microsoft.EntityFrameworkCore;

namespace OpenShort.Infrastructure.Data;

public class SqliteDbContext : AppDbContext
{
    // Design-time constructor
    public SqliteDbContext(DbContextOptions<SqliteDbContext> options) : base(options)
    {
    }
}
