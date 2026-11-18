using System.Globalization;
using MySqlConnector;

namespace OpenShort.Infrastructure.Data;

/// <summary>
/// Builds the MySQL connection string from the individual MYSQL_* settings.
/// Every value is escaped, so a password containing ';', '=' or quotes reaches the server as it is written
/// and cannot add or replace other connection options. Values are taken literally: quotes around a value
/// are part of it. Spaces around host, port, database, user and SSL mode are dropped, because they are
/// never meant; the password is left untouched.
/// </summary>
public static class MySqlConnectionStringFactory
{
    public const string PortSettingName = "MYSQL_PORT";
    public const string SslModeSettingName = "MYSQL_SSL_MODE";

    private const uint DefaultPort = 3306;
    private const string DefaultDatabase = "openshort";
    private const string DefaultUser = "root";
    // Plain by default, as it has always been: from the Linux image the TLS handshake with MySQL 5.7 fails,
    // so a TLS default would stop existing installations from starting
    private const MySqlSslMode DefaultSslMode = MySqlSslMode.None;

    private static readonly Dictionary<string, MySqlSslMode> SslModes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = MySqlSslMode.None,
        ["Preferred"] = MySqlSslMode.Preferred,
        ["Required"] = MySqlSslMode.Required,
        ["VerifyCA"] = MySqlSslMode.VerifyCA,
        ["VerifyFull"] = MySqlSslMode.VerifyFull
    };

    public static string Create(string host, string? port, string? database, string? user, string? password, string? sslMode = null)
    {
        var parsedSslMode = ParseSslMode(sslMode);

        var builder = new MySqlConnectionStringBuilder
        {
            Server = host.Trim(),
            Port = ParsePort(port),
            Database = string.IsNullOrWhiteSpace(database) ? DefaultDatabase : database.Trim(),
            UserID = string.IsNullOrWhiteSpace(user) ? DefaultUser : user.Trim(),
            Password = password ?? string.Empty,
            SslMode = parsedSslMode,
            // Without TLS, the default authentication of MySQL 8 (caching_sha2_password) needs the RSA key
            // of the server for the first sign in of an account. When TLS is mandatory it is never needed.
            AllowPublicKeyRetrieval = parsedSslMode is MySqlSslMode.None or MySqlSslMode.Preferred
        };

        return builder.ConnectionString;
    }

    private static uint ParsePort(string? port)
    {
        if (string.IsNullOrWhiteSpace(port))
        {
            return DefaultPort;
        }

        if (!ushort.TryParse(port.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort) || parsedPort == 0)
        {
            throw new InvalidOperationException(
                $"Invalid {PortSettingName} value '{port}': expected a number between 1 and 65535.");
        }

        return parsedPort;
    }

    private static MySqlSslMode ParseSslMode(string? sslMode)
    {
        if (string.IsNullOrWhiteSpace(sslMode))
        {
            return DefaultSslMode;
        }

        if (!SslModes.TryGetValue(sslMode.Trim(), out var parsedSslMode))
        {
            throw new InvalidOperationException(
                $"Invalid {SslModeSettingName} value '{sslMode}': expected one of {string.Join(", ", SslModes.Keys)}.");
        }

        return parsedSslMode;
    }
}
