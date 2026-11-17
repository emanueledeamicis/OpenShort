using System.Globalization;
using MySqlConnector;

namespace OpenShort.Infrastructure.Data;

/// <summary>
/// Builds the MySQL connection string from the individual MYSQL_* settings.
/// Every value is escaped, so a password containing ';', '=' or quotes reaches the server unchanged
/// and cannot add or replace other connection options.
/// </summary>
public static class MySqlConnectionStringFactory
{
    public const string PortSettingName = "MYSQL_PORT";

    private const uint DefaultPort = 3306;
    private const string DefaultDatabase = "openshort";
    private const string DefaultUser = "root";

    public static string Create(string host, string? port, string? database, string? user, string? password)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = ParsePort(port),
            Database = string.IsNullOrWhiteSpace(database) ? DefaultDatabase : database,
            UserID = string.IsNullOrWhiteSpace(user) ? DefaultUser : user,
            Password = password ?? string.Empty,
            SslMode = MySqlSslMode.None
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
}
