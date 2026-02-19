using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OpenShort.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteDbContext>, IDesignTimeDbContextFactory<MySqlDbContext>
{
    public SqliteDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteDbContext>();
        optionsBuilder.UseSqlite("Data Source=openshort.db");

        return new SqliteDbContext(optionsBuilder.Options);
    }

    MySqlDbContext IDesignTimeDbContextFactory<MySqlDbContext>.CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MySqlDbContext>();
        optionsBuilder.UseMySql("Server=localhost;Database=openshort;User=root;Password=root;", ServerVersion.Parse("8.0.30-mysql"));

        return new MySqlDbContext(optionsBuilder.Options);
    }
}
