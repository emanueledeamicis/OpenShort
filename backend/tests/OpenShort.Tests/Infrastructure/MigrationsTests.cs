using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Infrastructure;

/// <summary>
/// The migrations of each provider must describe the current model: a difference means that a migration
/// is missing, or that a provider upgrade changed how the model is mapped to the database.
/// No database is contacted.
/// </summary>
[TestFixture]
public class MigrationsTests
{
    [Test]
    public void MySqlMigrations_ShouldMatchTheModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MySqlDbContext>()
            .UseMySQL("Server=localhost;Database=openshort;User=openshort;Password=unused;")
            .Options;
        using var context = new MySqlDbContext(options);

        // Act
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Assert
        hasPendingChanges.Should().BeFalse();
    }

    [Test]
    public void SqliteMigrations_ShouldMatchTheModel()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqliteDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new SqliteDbContext(options);

        // Act
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Assert
        hasPendingChanges.Should().BeFalse();
    }
}
