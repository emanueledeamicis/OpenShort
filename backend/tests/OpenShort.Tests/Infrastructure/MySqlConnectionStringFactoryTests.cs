using FluentAssertions;
using MySql.Data.MySqlClient;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Infrastructure;

[TestFixture]
public class MySqlConnectionStringFactoryTests
{
    private const string Host = "db.internal";

    [Test]
    public void Create_ShouldSetEveryValue()
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, "3307", "shortener", "app", "S3cr3t!Pass", "Required"));

        // Assert
        result.Server.Should().Be(Host);
        result.Port.Should().Be(3307);
        result.Database.Should().Be("shortener");
        result.UserID.Should().Be("app");
        result.Password.Should().Be("S3cr3t!Pass");
        result.SslMode.Should().Be(MySqlSslMode.Required);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Create_ShouldUseDefaults_WhenOptionalValuesAreMissing(string? missing)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, missing, missing, missing, password: null));

        // Assert
        result.Port.Should().Be(3306);
        result.Database.Should().Be("openshort");
        result.UserID.Should().Be("root");
        result.Password.Should().BeEmpty();
    }

    // The password must reach the server exactly as configured
    [TestCase("a;b=c")]
    [TestCase("semi;colon")]
    [TestCase("equals=sign")]
    [TestCase("single'quote")]
    [TestCase("double\"quote")]
    [TestCase("both'and\"quotes")]
    [TestCase(" leading space")]
    [TestCase("trailing space ")]
    [TestCase("'quoted'")]
    [TestCase("\"quoted\"")]
    [TestCase("back\\slash")]
    [TestCase("{braces}")]
    [TestCase("p@ss;w=o'r\"d ")]
    [TestCase(";")]
    public void Create_ShouldKeepThePasswordUnchanged_WhenItContainsSpecialCharacters(string password)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, "3306", "openshort", "app", password));

        // Assert
        result.Password.Should().Be(password);
        result.Server.Should().Be(Host);
        result.Database.Should().Be("openshort");
        result.UserID.Should().Be("app");
    }

    // A value must not be able to add or replace other connection options
    [TestCase("x;SslMode=Required")]
    [TestCase("x;AllowLoadLocalInfile=true")]
    [TestCase("x;Server=attacker.example;Port=1")]
    [TestCase("x';Server=attacker.example;'")]
    [TestCase("x\";Server=attacker.example;\"")]
    [TestCase("x;AllowPublicKeyRetrieval=false")]
    public void Create_ShouldNotLetThePasswordChangeOtherOptions(string password)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, "3306", "openshort", "app", password));

        // Assert
        result.Password.Should().Be(password);
        result.Server.Should().Be(Host);
        result.Port.Should().Be(3306);
        result.SslMode.Should().Be(MySqlSslMode.Disabled);
        result.AllowPublicKeyRetrieval.Should().BeTrue();
        result.AllowLoadLocalInfile.Should().BeFalse();
    }

    [Test]
    public void Create_ShouldNotLetHostDatabaseOrUserChangeOtherOptions()
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(
            "db.internal;AllowLoadLocalInfile=true",
            "3306",
            "openshort;SslMode=Required",
            "app;Password=other",
            "S3cr3t!Pass"));

        // Assert
        result.Server.Should().Be("db.internal;AllowLoadLocalInfile=true");
        result.Database.Should().Be("openshort;SslMode=Required");
        result.UserID.Should().Be("app;Password=other");
        result.Password.Should().Be("S3cr3t!Pass");
        result.SslMode.Should().Be(MySqlSslMode.Disabled);
        result.AllowLoadLocalInfile.Should().BeFalse();
    }

    [Test]
    public void Create_ShouldDropSpacesAroundHostDatabaseAndUser_ButNotAroundThePassword()
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(" db.internal ", " 3307 ", " shortener ", " app ", " S3cr3t!Pass "));

        // Assert
        result.Server.Should().Be("db.internal");
        result.Port.Should().Be(3307);
        result.Database.Should().Be("shortener");
        result.UserID.Should().Be("app");
        result.Password.Should().Be(" S3cr3t!Pass ");
    }

    [Test]
    public void Create_ShouldTakeQuotesAroundAValueLiterally()
    {
        // Act: Docker Compose passes the quotes of `- MYSQL_USER="app"` to the container as they are
        var result = Parse(MySqlConnectionStringFactory.Create("\"db.internal\"", "3306", "'shortener'", "\"app\"", "\"secret\""));

        // Assert
        result.Server.Should().Be("\"db.internal\"");
        result.Database.Should().Be("'shortener'");
        result.UserID.Should().Be("\"app\"");
        result.Password.Should().Be("\"secret\"");
    }

    [TestCase("abc")]
    [TestCase("\"3307\"")]
    [TestCase("+3307")]
    [TestCase("3306;SslMode=Required")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("65536")]
    [TestCase("33.06")]
    public void Create_ShouldThrowNamingTheSettingAndNotThePassword_WhenPortIsNotValid(string port)
    {
        // Act
        var act = () => MySqlConnectionStringFactory.Create(Host, port, "openshort", "app", "S3cr3t!Pass");

        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("MYSQL_PORT").And.Contain(port);
        exception.Message.Should().NotContain("S3cr3t!Pass");
    }

    [TestCase("1", 1u)]
    [TestCase(" 3307 ", 3307u)]
    [TestCase("65535", 65535u)]
    public void Create_ShouldAcceptAValidPort(string port, uint expectedPort)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, port, "openshort", "app", "S3cr3t!Pass"));

        // Assert
        result.Port.Should().Be(expectedPort);
    }

    // The connection is plain unless MYSQL_SSL_MODE asks for TLS, as in the previous versions
    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Create_ShouldNotUseTls_WhenSslModeIsMissing(string? sslMode)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, "3306", "openshort", "app", "S3cr3t!Pass", sslMode));

        // Assert
        result.SslMode.Should().Be(MySqlSslMode.Disabled);
    }

    [TestCase("None", MySqlSslMode.Disabled)]
    [TestCase("Preferred", MySqlSslMode.Preferred)]
    [TestCase("Required", MySqlSslMode.Required)]
    [TestCase("VerifyCA", MySqlSslMode.VerifyCA)]
    [TestCase("VerifyFull", MySqlSslMode.VerifyFull)]
    [TestCase("required", MySqlSslMode.Required)]
    [TestCase("VERIFYFULL", MySqlSslMode.VerifyFull)]
    [TestCase(" Required ", MySqlSslMode.Required)]
    public void Create_ShouldUseTheConfiguredSslMode(string sslMode, MySqlSslMode expectedSslMode)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, "3306", "openshort", "app", "S3cr3t!Pass", sslMode));

        // Assert
        result.SslMode.Should().Be(expectedSslMode);
    }

    // The first sign in of a MySQL 8 account needs TLS or the RSA key of the server
    [TestCase("None", true)]
    [TestCase("Preferred", true)]
    [TestCase("Required", false)]
    [TestCase("VerifyCA", false)]
    [TestCase("VerifyFull", false)]
    public void Create_ShouldAllowPublicKeyRetrieval_OnlyWhenTlsIsNotMandatory(string sslMode, bool expected)
    {
        // Act
        var result = Parse(MySqlConnectionStringFactory.Create(Host, "3306", "openshort", "app", "S3cr3t!Pass", sslMode));

        // Assert
        result.AllowPublicKeyRetrieval.Should().Be(expected);
    }

    [TestCase("abc")]
    [TestCase("\"Required\"")]
    [TestCase("1")]
    [TestCase("Disabled")]
    [TestCase("Verify-Full")]
    [TestCase("Required;AllowLoadLocalInfile=true")]
    public void Create_ShouldThrowNamingTheSettingAndNotThePassword_WhenSslModeIsNotValid(string sslMode)
    {
        // Act
        var act = () => MySqlConnectionStringFactory.Create(Host, "3306", "openshort", "app", "S3cr3t!Pass", sslMode);

        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("MYSQL_SSL_MODE").And.Contain(sslMode).And.Contain("VerifyFull");
        exception.Message.Should().NotContain("S3cr3t!Pass");
    }

    [Test]
    public void Create_ShouldProduceAStringTheLogDescriptionCanRead()
    {
        // Act
        var connectionString = MySqlConnectionStringFactory.Create(Host, "3307", "shortener", "app", "a;b=c");
        var description = DatabaseConnectionDescriber.Describe(connectionString, useMySql: true);

        // Assert
        description.Should().Be("MySQL (server: db.internal, port: 3307, database: shortener, ssl mode: None)");
    }

    private static MySqlConnectionStringBuilder Parse(string connectionString) => new(connectionString);

    [TestCase("Server=db.internal;Password")]
    [TestCase("Server=db.internal;Password='S3cr3t!Pass")]
    [TestCase("Server=db.internal;User=shortener;Password=first;S3cr3t!Pass")]
    [TestCase("Server=db.internal;Password=S3cr3t!Pass;SslMode=None")]
    [TestCase("Server=db.internal;Password=S3cr3t!Pass;Port=abc")]
    public void EnsureReadable_ShouldThrowWithoutExposingThePassword_WhenTheConnectionStringCannotBeRead(string connectionString)
    {
        // Act
        var act = () => MySqlConnectionStringFactory.EnsureReadable(connectionString);

        // Assert
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.ToString().Should().NotContain("S3cr3t!Pass");
        exception.InnerException.Should().BeNull();
    }

    [Test]
    public void EnsureReadable_ShouldAcceptAConnectionStringBuiltFromTheSettings()
    {
        // Arrange
        var connectionString = MySqlConnectionStringFactory.Create("db.internal", "3306", "openshort", "shortener", "pa;ss='word");

        // Act
        var act = () => MySqlConnectionStringFactory.EnsureReadable(connectionString);

        // Assert
        act.Should().NotThrow();
    }
}
