using Microsoft.Data.SqlClient;

namespace VentasExport.Configuration;

/// <summary>Configuración leída de variables de entorno (o de un archivo .env en desarrollo).</summary>
public sealed class Settings
{
    // SQL Server
    public string SqlConnectionString => BuildConnectionString();
    public int SqlCommandTimeoutSeconds { get; init; }

    // Consultas
    public required string SqlDirectory { get; init; }
    public required HashSet<string> SqlExclude { get; init; }

    // Excel
    public required string OutputDirectory { get; init; }

    // Google Drive (OAuth de usuario: la carpeta es compartida, no una Unidad compartida)
    public string? GoogleClientId { get; init; }
    public string? GoogleClientSecret { get; init; }
    public string? GoogleRefreshToken { get; init; }
    public string? DriveFolderId { get; init; }

    public static Settings Load()
    {
        return new Settings
        {
            SqlCommandTimeoutSeconds = int.Parse(Optional("SQL_COMMAND_TIMEOUT", "600")),
            SqlDirectory = Optional("SQL_DIR", Path.Combine(AppContext.BaseDirectory, "sql")),
            SqlExclude = Optional("SQL_EXCLUDE", "ventas.sql")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            OutputDirectory = Optional("OUTPUT_DIR", Path.Combine(AppContext.BaseDirectory, "output")),
            GoogleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
            GoogleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET"),
            GoogleRefreshToken = Environment.GetEnvironmentVariable("GOOGLE_REFRESH_TOKEN"),
            DriveFolderId = Environment.GetEnvironmentVariable("DRIVE_FOLDER_ID"),
        };
    }

    // Se evalúa al usarse, para que el comando 'auth' no exija datos de SQL.
    private static string BuildConnectionString() => new SqlConnectionStringBuilder
    {
        DataSource = Required("SQL_SERVER"),
        InitialCatalog = Optional("SQL_DATABASE", "DataIA"),
        UserID = Required("SQL_USER"),
        Password = Required("SQL_PASSWORD"),
        Encrypt = Bool("SQL_ENCRYPT", true),
        TrustServerCertificate = Bool("SQL_TRUST_SERVER_CERTIFICATE", true),
        ApplicationName = "VentasExport",
    }.ConnectionString;

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : throw new InvalidOperationException($"Falta la variable de entorno {name}");

    private static string Optional(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    private static bool Bool(string name, bool fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? bool.Parse(v) : fallback;
}
