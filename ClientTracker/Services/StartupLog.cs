namespace ClientTracker.Services;

public static class StartupLog
{
    private static readonly object Gate = new();

    public static string LogPath
    {
        get
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClientTracker");
            return Path.Combine(baseDir, "startup.log");
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // best-effort
        }
    }

    public static void Write(Exception exception, string? context = null)
    {
        var header = context is null ? "Unhandled exception" : $"Unhandled exception ({context})";
        Write($"{header}: {exception}");
    }
}
