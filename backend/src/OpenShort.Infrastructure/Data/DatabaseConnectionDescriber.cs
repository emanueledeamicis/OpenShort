using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace OpenShort.Infrastructure.Data;

/// <summary>
/// Builds a description of the configured database that is safe to write to logs:
/// it never includes the user name or the password.
/// </summary>
public static class DatabaseConnectionDescriber
{
    private const string UnreadableDetails = "connection details could not be read";

    public static string Describe(string? connectionString, bool useMySql)
    {
        return useMySql
            ? $"MySQL ({DescribeMySql(connectionString)})"
            : $"SQLite ({DescribeSqlite(connectionString)})";
    }

    private static string DescribeMySql(string? connectionString)
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(connectionString ?? string.Empty);
            // "Disabled" is shown with the name MYSQL_SSL_MODE uses for it
            var sslMode = builder.SslMode == MySqlSslMode.Disabled ? "None" : builder.SslMode.ToString();
            return $"server: {builder.Server}, port: {builder.Port}, database: {builder.Database}, ssl mode: {sslMode}";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            // Never fall back to the raw connection string: it may contain the password
            return UnreadableDetails;
        }
    }

    private static string DescribeSqlite(string? connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString ?? string.Empty);
            return $"data source: {builder.DataSource}";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            // Never fall back to the raw connection string: it may contain the password
            return UnreadableDetails;
        }
    }
}
