using FluentAssertions;
using NUnit.Framework;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Infrastructure;

[TestFixture]
public class DatabaseConnectionDescriberTests
{
    [Test]
    public void Describe_ShouldReturnServerPortDatabaseAndSslMode_ForMySql()
    {
        // Arrange
        var connectionString = "Server=db.internal;Port=3307;Database=openshort;User=shortener;Password=S3cr3t!Pass;SslMode=Disabled;";

        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: true);

        // Assert
        description.Should().Be("MySQL (server: db.internal, port: 3307, database: openshort, ssl mode: None)");
    }

    [TestCase("Server=db.internal;Port=3306;Database=openshort;User=shortener;Password=S3cr3t!Pass;SslMode=Disabled;")]
    [TestCase("server=db.internal;database=openshort;uid=shortener;pwd=S3cr3t!Pass")]
    [TestCase("Host=db.internal;Initial Catalog=openshort;User Id=shortener;Password='S3cr3t!Pass'")]
    public void Describe_ShouldNotExposeCredentials_ForMySql(string connectionString)
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: true);

        // Assert
        description.Should().Contain("db.internal").And.Contain("openshort");
        description.Should().NotContain("S3cr3t!Pass");
        description.Should().NotContain("shortener");
    }

    [Test]
    public void Describe_ShouldReturnDataSource_ForSqlite()
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe("Data Source=data/openshort.db", useMySql: false);

        // Assert
        description.Should().Be("SQLite (data source: data/openshort.db)");
    }

    [Test]
    public void Describe_ShouldNotExposePassword_ForSqlite()
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe("Data Source=data/openshort.db;Password=S3cr3t!Pass", useMySql: false);

        // Assert
        description.Should().Be("SQLite (data source: data/openshort.db)");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Describe_ShouldNotExposeRawConnectionString_WhenItCannotBeParsed(bool useMySql)
    {
        // Arrange
        var connectionString = "Server=db.internal;NotAnOption=1;Password=S3cr3t!Pass";

        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql);

        // Assert
        description.Should().Contain("connection details could not be read");
        description.Should().NotContain("S3cr3t!Pass");
    }

    [TestCase("Data Source=data/openshort.db;Default Timeout=abc;Password=S3cr3t!Pass", false)]
    [TestCase("Data Source=data/openshort.db;Pooling=maybe;Password=S3cr3t!Pass", false)]
    [TestCase("Server=db.internal;Port=abc;Password=S3cr3t!Pass", true)]
    [TestCase("Server=db.internal;SslMode=None;Password=S3cr3t!Pass", true)]
    [TestCase("Server=db.internal;Pooling=maybe;Password=S3cr3t!Pass", true)]
    public void Describe_ShouldNotThrowOrExposePassword_WhenAnOptionValueIsInvalid(string connectionString, bool useMySql)
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql);

        // Assert
        description.Should().Contain("connection details could not be read");
        description.Should().NotContain("S3cr3t!Pass");
    }

    [TestCase("Server=db.internal;Password")]
    [TestCase("Server=db.internal;Password='S3cr3t!Pass")]
    [TestCase("Server=db.internal;User=shortener;Password=first;S3cr3t!Pass")]
    public void Describe_ShouldNotThrowOrExposePassword_WhenTheMySqlConnectionStringIsMalformed(string connectionString)
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: true);

        // Assert
        description.Should().Be("MySQL (connection details could not be read)");
    }

    [TestCase(null)]
    [TestCase("")]
    public void Describe_ShouldReturnDefaults_WhenMySqlConnectionStringIsMissing(string? connectionString)
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: true);

        // Assert
        description.Should().Be("MySQL (server: , port: 3306, database: , ssl mode: Preferred)");
    }

    [TestCase(null)]
    [TestCase("")]
    public void Describe_ShouldReturnEmptyDataSource_WhenSqliteConnectionStringIsMissing(string? connectionString)
    {
        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: false);

        // Assert
        description.Should().Be("SQLite (data source: )");
    }
}
