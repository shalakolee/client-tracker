using System.Globalization;
using System.Linq;
using ClientTracker.Services;

namespace ClientTracker.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly LocalizationResourceManager _localization;
    private readonly DatabaseService _database;
    private readonly UpdateService _updateService;
    private string _databasePath = string.Empty;
    private string _activeClientCount = string.Empty;
    private string _gitHubOwner = string.Empty;
    private string _gitHubRepo = string.Empty;
    private string _windowsAssetPattern = string.Empty;
    private string _macAssetPattern = string.Empty;
    private string _androidAssetPattern = string.Empty;
    private bool _includePreReleases;
    private bool _requireUpdates;

    public SettingsViewModel(LocalizationResourceManager localization, DatabaseService database, UpdateService updateService)
    {
        _localization = localization;
        _database = database;
        _updateService = updateService;
        Languages = new List<LanguageOption>
        {
            new("en-US", "Settings_English"),
            new("pt-BR", "Settings_Portuguese")
        };

        _selectedLanguage = Languages.FirstOrDefault(l => l.CultureName == _localization.CurrentCulture.Name) ?? Languages[0];
        ApplyLanguageCommand = new Command(() => ApplyLanguage(SelectedLanguage));
        RefreshDiagnosticsCommand = new Command(async () => await RefreshDiagnosticsAsync());
        SaveUpdateSettingsCommand = new Command(SaveUpdateSettings);
        CheckUpdatesCommand = new Command(async () => await _updateService.CheckForUpdatesAsync(true));
        LoadUpdateSettings();
        _ = RefreshDiagnosticsAsync();
    }

    public List<LanguageOption> Languages { get; }

    private LanguageOption _selectedLanguage;
    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public Command ApplyLanguageCommand { get; }
    public Command RefreshDiagnosticsCommand { get; }
    public Command SaveUpdateSettingsCommand { get; }
    public Command CheckUpdatesCommand { get; }

    public string DatabasePath
    {
        get => _databasePath;
        set => SetProperty(ref _databasePath, value);
    }

    public string ActiveClientCount
    {
        get => _activeClientCount;
        set => SetProperty(ref _activeClientCount, value);
    }

    public string GitHubOwner
    {
        get => _gitHubOwner;
        set => SetProperty(ref _gitHubOwner, value);
    }

    public string GitHubRepo
    {
        get => _gitHubRepo;
        set => SetProperty(ref _gitHubRepo, value);
    }

    public string WindowsAssetPattern
    {
        get => _windowsAssetPattern;
        set => SetProperty(ref _windowsAssetPattern, value);
    }

    public string MacAssetPattern
    {
        get => _macAssetPattern;
        set => SetProperty(ref _macAssetPattern, value);
    }

    public string AndroidAssetPattern
    {
        get => _androidAssetPattern;
        set => SetProperty(ref _androidAssetPattern, value);
    }

    public bool IncludePreReleases
    {
        get => _includePreReleases;
        set => SetProperty(ref _includePreReleases, value);
    }

    public bool RequireUpdates
    {
        get => _requireUpdates;
        set => SetProperty(ref _requireUpdates, value);
    }

    public void ApplyLanguage(LanguageOption? option)
    {
        if (option is null)
        {
            return;
        }

        var culture = new CultureInfo(option.CultureName);
        _localization.SetCulture(culture);
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var window = app.Windows.FirstOrDefault();
        if (window is not null)
        {
            window.Page = new AppShell();
        }
    }

    private async Task RefreshDiagnosticsAsync()
    {
        DatabasePath = _database.DatabasePath;
        var count = await _database.GetActiveClientCountAsync();
        ActiveClientCount = count.ToString();
    }

    private void LoadUpdateSettings()
    {
        var settings = UpdateSettings.Load();
        GitHubOwner = settings.GitHubOwner;
        GitHubRepo = settings.GitHubRepo;
        WindowsAssetPattern = settings.WindowsAssetPattern;
        MacAssetPattern = settings.MacAssetPattern;
        AndroidAssetPattern = settings.AndroidAssetPattern;
        IncludePreReleases = settings.IncludePreReleases;
        RequireUpdates = settings.RequireUpdates;
    }

    private void SaveUpdateSettings()
    {
        var settings = new UpdateSettings
        {
            GitHubOwner = GitHubOwner,
            GitHubRepo = GitHubRepo,
            WindowsAssetPattern = WindowsAssetPattern,
            MacAssetPattern = MacAssetPattern,
            AndroidAssetPattern = AndroidAssetPattern,
            IncludePreReleases = IncludePreReleases,
            RequireUpdates = RequireUpdates
        };

        settings.Save();
    }
}

public record LanguageOption(string CultureName, string DisplayKey);
