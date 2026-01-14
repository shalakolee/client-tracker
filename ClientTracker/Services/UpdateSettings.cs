using Microsoft.Maui.Storage;

namespace ClientTracker.Services;

public class UpdateSettings
{
    private const string OwnerKey = "Update.GitHubOwner";
    private const string RepoKey = "Update.GitHubRepo";
    private const string WindowsPatternKey = "Update.WindowsAssetPattern";
    private const string MacPatternKey = "Update.MacAssetPattern";
    private const string AndroidPatternKey = "Update.AndroidAssetPattern";
    private const string IncludePreReleaseKey = "Update.IncludePreRelease";
    private const string RequireUpdatesKey = "Update.RequireUpdates";

    public string GitHubOwner { get; set; } = string.Empty;
    public string GitHubRepo { get; set; } = string.Empty;
    public string WindowsAssetPattern { get; set; } = "win-x64;windows;zip;msix;exe";
    public string MacAssetPattern { get; set; } = "maccatalyst;mac;zip;dmg;pkg";
    public string AndroidAssetPattern { get; set; } = "android;apk";
    public bool IncludePreReleases { get; set; }
    public bool RequireUpdates { get; set; }

    public bool HasValidConfiguration =>
        !string.IsNullOrWhiteSpace(GitHubOwner) && !string.IsNullOrWhiteSpace(GitHubRepo);

    public static UpdateSettings Load()
    {
        return new UpdateSettings
        {
            GitHubOwner = Preferences.Get(OwnerKey, string.Empty),
            GitHubRepo = Preferences.Get(RepoKey, string.Empty),
            WindowsAssetPattern = Preferences.Get(WindowsPatternKey, "win-x64;windows;zip;msix;exe"),
            MacAssetPattern = Preferences.Get(MacPatternKey, "maccatalyst;mac;zip;dmg;pkg"),
            AndroidAssetPattern = Preferences.Get(AndroidPatternKey, "android;apk"),
            IncludePreReleases = Preferences.Get(IncludePreReleaseKey, false),
            RequireUpdates = Preferences.Get(RequireUpdatesKey, false)
        };
    }

    public void Save()
    {
        Preferences.Set(OwnerKey, GitHubOwner?.Trim() ?? string.Empty);
        Preferences.Set(RepoKey, GitHubRepo?.Trim() ?? string.Empty);
        Preferences.Set(WindowsPatternKey, WindowsAssetPattern?.Trim() ?? string.Empty);
        Preferences.Set(MacPatternKey, MacAssetPattern?.Trim() ?? string.Empty);
        Preferences.Set(AndroidPatternKey, AndroidAssetPattern?.Trim() ?? string.Empty);
        Preferences.Set(IncludePreReleaseKey, IncludePreReleases);
        Preferences.Set(RequireUpdatesKey, RequireUpdates);
    }
}
