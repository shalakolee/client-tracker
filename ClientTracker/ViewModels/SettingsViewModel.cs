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
    private bool _useRemoteMySql;
    private string _dbHost = string.Empty;
    private string _dbPort = string.Empty;
    private string _dbName = string.Empty;
    private string _dbUsername = string.Empty;
    private string _dbPassword = string.Empty;
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
        SaveDatabaseConnectionCommand = new Command(async () => await SaveDatabaseConnectionAsync());
        TestDatabaseConnectionCommand = new Command(async () => await TestDatabaseConnectionAsync());
        SyncFromRemoteDatabaseCommand = new Command(async () => await SyncFromRemoteDatabaseAsync());
        PushLocalToRemoteDatabaseCommand = new Command(async () => await PushLocalToRemoteDatabaseAsync());
        CheckUpdatesCommand = new Command(async () => await _updateService.CheckForUpdatesAsync(true));
        LoadUpdateSettings();
        _ = LoadDatabaseConnectionAsync();
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
    public Command SaveDatabaseConnectionCommand { get; }
    public Command TestDatabaseConnectionCommand { get; }
    public Command SyncFromRemoteDatabaseCommand { get; }
    public Command PushLocalToRemoteDatabaseCommand { get; }
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

    public bool UseRemoteMySql
    {
        get => _useRemoteMySql;
        set => SetProperty(ref _useRemoteMySql, value);
    }

    public string DbHost
    {
        get => _dbHost;
        set => SetProperty(ref _dbHost, value);
    }

    public string DbPort
    {
        get => _dbPort;
        set => SetProperty(ref _dbPort, value);
    }

    public string DbName
    {
        get => _dbName;
        set => SetProperty(ref _dbName, value);
    }

    public string DbUsername
    {
        get => _dbUsername;
        set => SetProperty(ref _dbUsername, value);
    }

    public string DbPassword
    {
        get => _dbPassword;
        set => SetProperty(ref _dbPassword, value);
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
            window.Page = ShellFactory.CreateShell();
        }
    }

    private async Task RefreshDiagnosticsAsync()
    {
        DatabasePath = _database.DatabasePath;
        var count = await _database.GetActiveClientCountAsync();
        ActiveClientCount = count.ToString();
    }

    private async Task LoadDatabaseConnectionAsync()
    {
        var settings = await DatabaseConnectionSettings.LoadAsync();
        UseRemoteMySql = settings.UseRemoteMySql;
        DbHost = string.IsNullOrWhiteSpace(settings.Host) ? DatabaseConnectionSettings.DefaultHost : settings.Host;
        DbPort = settings.Port.ToString(CultureInfo.InvariantCulture);
        DbName = string.IsNullOrWhiteSpace(settings.Database) ? DatabaseConnectionSettings.DefaultDatabase : settings.Database;
        DbUsername = settings.Username;
        DbPassword = settings.Password;
    }

    private async Task SaveDatabaseConnectionAsync()
    {
        var settings = await DatabaseConnectionSettings.LoadAsync();
        settings.UseRemoteMySql = UseRemoteMySql;
        settings.Host = DbHost;
        settings.Database = DbName;
        settings.Username = DbUsername;
        settings.Password = DbPassword;
        if (int.TryParse(DbPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            settings.Port = port;
        }

        await settings.SaveAsync();
        await _database.ReloadRemoteSettingsAsync();
        await RefreshDiagnosticsAsync();

        var ok = LocalizationResourceManager.Instance["Action_OK"];
        await Shell.Current.DisplayAlertAsync(
            LocalizationResourceManager.Instance["Title_Settings"],
            LocalizationResourceManager.Instance["Settings_DbSaved"],
            ok);
    }

    private async Task TestDatabaseConnectionAsync()
    {
        var ok = await _database.TestRemoteMySqlConnectionAsync();
        var title = LocalizationResourceManager.Instance["Title_Settings"];
        var message = ok
            ? LocalizationResourceManager.Instance["Settings_DbConnectionOk"]
            : LocalizationResourceManager.Instance["Settings_DbConnectionFailed"];
        await Shell.Current.DisplayAlertAsync(title, message, LocalizationResourceManager.Instance["Action_OK"]);
    }

    private async Task SyncFromRemoteDatabaseAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            LocalizationResourceManager.Instance["Title_Settings"],
            LocalizationResourceManager.Instance["Settings_DbSyncFromRemoteConfirm"],
            LocalizationResourceManager.Instance["Action_Continue"],
            LocalizationResourceManager.Instance["Action_Cancel"]);

        if (!confirm)
        {
            return;
        }

        await _database.SyncFromRemoteMySqlAsync(replaceLocal: true);
        await RefreshDiagnosticsAsync();
        await Shell.Current.DisplayAlertAsync(
            LocalizationResourceManager.Instance["Title_Settings"],
            LocalizationResourceManager.Instance["Settings_DbSyncComplete"],
            LocalizationResourceManager.Instance["Action_OK"]);
    }

    private async Task PushLocalToRemoteDatabaseAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            LocalizationResourceManager.Instance["Title_Settings"],
            LocalizationResourceManager.Instance["Settings_DbPushToRemoteConfirm"],
            LocalizationResourceManager.Instance["Action_Continue"],
            LocalizationResourceManager.Instance["Action_Cancel"]);

        if (!confirm)
        {
            return;
        }

        await _database.PushLocalDataToRemoteMySqlAsync(overwriteRemote: true);
        await Shell.Current.DisplayAlertAsync(
            LocalizationResourceManager.Instance["Title_Settings"],
            LocalizationResourceManager.Instance["Settings_DbPushComplete"],
            LocalizationResourceManager.Instance["Action_OK"]);
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
}

public record LanguageOption(string CultureName, string DisplayKey);
