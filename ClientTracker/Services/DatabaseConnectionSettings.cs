using System.Globalization;

namespace ClientTracker.Services;

public sealed class DatabaseConnectionSettings
{
    private const string Prefix = "ClientTracker.Db.";
    private const string KeyUseRemote = Prefix + "UseRemoteMySql";
    private const string KeyHost = Prefix + "Host";
    private const string KeyPort = Prefix + "Port";
    private const string KeyDatabase = Prefix + "Database";
    private const string KeyUsername = Prefix + "Username";
    private const string KeyPassword = Prefix + "Password";

    public const string DefaultHost = "ak-client-tracker.cav0ua408pj3.us-east-1.rds.amazonaws.com";
    public const int DefaultPort = 3306;
    public const string DefaultDatabase = "ak_client_tracker";

    public bool UseRemoteMySql { get; set; }
    public string Host { get; set; } = DefaultHost;
    public int Port { get; set; } = DefaultPort;
    public string Database { get; set; } = DefaultDatabase;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public static async Task<DatabaseConnectionSettings> LoadAsync()
    {
        var settings = new DatabaseConnectionSettings
        {
            UseRemoteMySql = Preferences.Get(KeyUseRemote, false),
            Host = Preferences.Get(KeyHost, DefaultHost),
            Port = Preferences.Get(KeyPort, DefaultPort),
            Database = Preferences.Get(KeyDatabase, DefaultDatabase),
            Username = Preferences.Get(KeyUsername, string.Empty),
        };

        try
        {
            settings.Password = await SecureStorage.GetAsync(KeyPassword) ?? string.Empty;
        }
        catch
        {
            settings.Password = string.Empty;
        }

        settings.Host = settings.Host?.Trim() ?? string.Empty;
        settings.Database = settings.Database?.Trim() ?? string.Empty;
        settings.Username = settings.Username?.Trim() ?? string.Empty;
        return settings;
    }

    public async Task SaveAsync()
    {
        Host = Host?.Trim() ?? string.Empty;
        Database = Database?.Trim() ?? string.Empty;
        Username = Username?.Trim() ?? string.Empty;

        Preferences.Set(KeyUseRemote, UseRemoteMySql);
        Preferences.Set(KeyHost, Host);
        Preferences.Set(KeyPort, Port);
        Preferences.Set(KeyDatabase, Database);
        Preferences.Set(KeyUsername, Username);

        try
        {
            await SecureStorage.SetAsync(KeyPassword, Password ?? string.Empty);
        }
        catch
        {
            // Best-effort. If secure storage isn't available on the device, the user can re-enter later.
        }
    }

    public string GetDisplayName()
    {
        if (!UseRemoteMySql)
        {
            return "SQLite";
        }

        var host = string.IsNullOrWhiteSpace(Host) ? "?" : Host;
        var port = Port <= 0 ? DefaultPort : Port;
        var db = string.IsNullOrWhiteSpace(Database) ? "?" : Database;
        return string.Create(CultureInfo.InvariantCulture, $"MySQL {host}:{port}/{db}");
    }

    public string? BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Host) ||
            string.IsNullOrWhiteSpace(Database) ||
            string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
        {
            return null;
        }

        var port = Port <= 0 ? DefaultPort : Port;
        return $"Server={Host};Port={port};Database={Database};User ID={Username};Password={Password};SslMode=Preferred;";
    }
}

