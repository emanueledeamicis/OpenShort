using FluentAssertions;
using NUnit.Framework;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Infrastructure;

[TestFixture]
public class DatabaseConnectionDescriberTests
{
    [Test]
    public void Describe_ShouldReturnServerPortAndDatabase_ForMySql()
    {
        // Arrange
        var connectionString = "Server=db.internal;Port=3307;Database=openshort;User=shortener;Password=S3cr3t!Pass;SslMode=None;";

        // Act
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: true);

        // Assert
        description.Should().Be("MySQL (server: db.internal, port: 3307, database: openshort)");
    }

    [TestCase("Server=db.internal;Port=3306;Database=openshort;User=shortener;Password=S3cr3t!Pass;SslMode=None;")]
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

    [TestCase(true)]
    [TestCase(false)]
    public void Describe_ShouldNotThrow_WhenConnectionStringIsNull(bool useMySql)
    {
        // Act
        var act = () => DatabaseConnectionDescriber.Describe(null, useMySql);

        // Assert
        act.Should().NotThrow();
    }
}
