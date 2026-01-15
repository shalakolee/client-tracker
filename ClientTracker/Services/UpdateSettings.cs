namespace ClientTracker.Services;

public class UpdateSettings
{
    public const string DefaultGitHubOwner = "shalakolee";
    public const string DefaultGitHubRepo = "client-tracker";

    public const string DefaultWindowsPattern = "ClientTracker-windows.msix;msix";
    public const string DefaultMacPattern = "ClientTracker-maccatalyst.dmg;dmg";
    public const string DefaultAndroidPattern = "ClientTracker-android.apk;apk";

    public string GitHubOwner { get; set; } = DefaultGitHubOwner;
    public string GitHubRepo { get; set; } = DefaultGitHubRepo;
    public string WindowsAssetPattern { get; set; } = DefaultWindowsPattern;
    public string MacAssetPattern { get; set; } = DefaultMacPattern;
    public string AndroidAssetPattern { get; set; } = DefaultAndroidPattern;
    public bool IncludePreReleases { get; set; } = false;
    public bool RequireUpdates { get; set; } = false;

    public bool HasValidConfiguration => true;

    public static UpdateSettings Load()
    {
        return new UpdateSettings();
    }

    public void Save()
    {
        // Intentionally disabled: update source/patterns are app-controlled.
    }
}
