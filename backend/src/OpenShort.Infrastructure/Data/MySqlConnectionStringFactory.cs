using System.Globalization;
using MySqlConnector;

namespace OpenShort.Infrastructure.Data;

/// <summary>
/// Builds the MySQL connection string from the individual MYSQL_* settings.
/// Every value is escaped, so a password containing ';', '=' or quotes reaches the server as it is written
/// and cannot add or replace other connection options. Values are taken literally: quotes around a value
/// are part of it. Spaces around host, port, database and user are dropped, because they are never meant;
/// the password is left untouched.
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
            Server = host.Trim(),
            Port = ParsePort(port),
            Database = string.IsNullOrWhiteSpace(database) ? DefaultDatabase : database.Trim(),
            UserID = string.IsNullOrWhiteSpace(user) ? DefaultUser : user.Trim(),
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
