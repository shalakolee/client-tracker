using System.Net.Http.Headers;
using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using PathIO = System.IO.Path;

namespace ClientTracker.Services;

public class UpdateService : INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private readonly LocalizationResourceManager _localization;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private GitHubRelease? _availableRelease;
    private bool _isChecking;
    private bool _isUpdateAvailable;
    private string _availableVersion = string.Empty;
    private string _availableNotes = string.Empty;
    private string _availableUrl = string.Empty;
    private DateTimeOffset? _lastChecked;
    private CancellationTokenSource? _backgroundCts;

    public static UpdateService? Instance { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpdateService(HttpClient httpClient, LocalizationResourceManager localization)
    {
        _httpClient = httpClient;
        _localization = localization;
        Instance = this;
    }

    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set => SetProperty(ref _isUpdateAvailable, value);
    }

    public string AvailableVersion
    {
        get => _availableVersion;
        private set => SetProperty(ref _availableVersion, value);
    }

    public string AvailableNotes
    {
        get => _availableNotes;
        private set => SetProperty(ref _availableNotes, value);
    }

    public string AvailableUrl
    {
        get => _availableUrl;
        private set => SetProperty(ref _availableUrl, value);
    }

    public DateTimeOffset? LastChecked
    {
        get => _lastChecked;
        private set => SetProperty(ref _lastChecked, value);
    }

    public void StartBackgroundChecks(TimeSpan? interval = null)
    {
        if (_backgroundCts is not null)
        {
            return;
        }

        _backgroundCts = new CancellationTokenSource();
        var loopInterval = interval ?? TimeSpan.FromHours(6);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), _backgroundCts.Token);
            while (!_backgroundCts.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdatesInBackgroundAsync(_backgroundCts.Token);
                }
                catch (Exception ex)
                {
                    StartupLog.Write(ex, "UpdateService.Background");
                }

                try
                {
                    await Task.Delay(loopInterval, _backgroundCts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, _backgroundCts.Token);
    }

    public void StopBackgroundChecks()
    {
        _backgroundCts?.Cancel();
        _backgroundCts?.Dispose();
        _backgroundCts = null;
    }

    public Task CheckForUpdatesInBackgroundAsync(CancellationToken cancellationToken = default)
        => CheckForUpdatesInternalAsync(showIfNoUpdate: false, showPromptIfUpdate: false, cancellationToken);

    public async Task CheckForUpdatesAsync(bool showIfNoUpdate)
        => await CheckForUpdatesInternalAsync(showIfNoUpdate, showPromptIfUpdate: true, CancellationToken.None);

    public async Task ShowUpdatePromptIfAvailableAsync()
    {
        var settings = UpdateSettings.Load();
        if (_availableRelease is null)
        {
            await CheckForUpdatesInternalAsync(showIfNoUpdate: true, showPromptIfUpdate: true, CancellationToken.None);
            return;
        }

        await PromptAndMaybeInstallAsync(_availableRelease, settings);
    }

    private async Task CheckForUpdatesInternalAsync(bool showIfNoUpdate, bool showPromptIfUpdate, CancellationToken cancellationToken)
    {
        var settings = UpdateSettings.Load();
        if (!settings.HasValidConfiguration)
        {
            if (showIfNoUpdate)
            {
                await ShowAlertAsync(_localization["Update_Title"], _localization["Update_NotConfigured"]);
            }
            return;
        }

        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            IsChecking = true;

            var release = await GetLatestReleaseAsync(settings);
            LastChecked = DateTimeOffset.UtcNow;
            if (release is null)
            {
                if (showIfNoUpdate)
                {
                    await ShowAlertAsync(_localization["Update_Title"], _localization["Update_NotFound"]);
                }
                return;
            }

            if (!IsNewerVersion(release.TagName, AppInfo.VersionString))
            {
                IsUpdateAvailable = false;
                AvailableVersion = string.Empty;
                AvailableNotes = string.Empty;
                AvailableUrl = string.Empty;
                _availableRelease = null;

                if (showIfNoUpdate)
                {
                    await ShowAlertAsync(_localization["Update_Title"], _localization["Update_NoUpdate"]);
                }

                return;
            }

            IsUpdateAvailable = true;
            AvailableVersion = release.DisplayName;
            AvailableNotes = release.Notes;
            AvailableUrl = release.HtmlUrl;
            _availableRelease = release;

            if (!showPromptIfUpdate)
            {
                return;
            }

            await PromptAndMaybeInstallAsync(release, settings);
        }
        finally
        {
            IsChecking = false;
            _checkLock.Release();
        }
    }

    private async Task PromptAndMaybeInstallAsync(GitHubRelease release, UpdateSettings settings)
    {
        var choice = await ShowUpdatePromptAsync(release, settings.RequireUpdates);
        if (choice == UpdateChoice.ViewNotes && !string.IsNullOrWhiteSpace(release.HtmlUrl))
        {
            await Launcher.OpenAsync(new Uri(release.HtmlUrl));
            return;
        }

        if (choice != UpdateChoice.Download)
        {
            return;
        }

        var assetUrl = SelectAssetUrl(release.Assets, settings);
        if (string.IsNullOrWhiteSpace(assetUrl))
        {
            await ShowAlertAsync(_localization["Update_Title"], _localization["Update_NoAsset"]);
            return;
        }

        await DownloadAndLaunchAsync(release.TagName, assetUrl);
    }

    private async Task DownloadAndLaunchAsync(string releaseTag, string url)
    {
        var page = CreateProgressPage(out var statusLabel, out var progressBar);
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.Navigation.PushModalAsync(page));

        var fileName = PathIO.GetFileName(new Uri(url).LocalPath);
        var targetPath = PathIO.Combine(FileSystem.CacheDirectory, fileName);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        await using (var stream = await response.Content.ReadAsStreamAsync())
        await using (var file = File.Create(targetPath))
        {
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;
                if (contentLength.HasValue && contentLength.Value > 0)
                {
                    var progress = (double)totalRead / contentLength.Value;
                    await UpdateProgressAsync(statusLabel, progressBar, progress);
                }
            }
        }

        await UpdateStatusAsync(statusLabel, _localization["Update_Preparing"]);

        if (string.Equals(PathIO.GetExtension(targetPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            var updateRoot = PathIO.Combine(FileSystem.AppDataDirectory, "updates", SanitizeFolderName(releaseTag));
            Directory.CreateDirectory(updateRoot);

            await UpdateStatusAsync(statusLabel, _localization["Update_Extracting"]);
            try
            {
                if (Directory.Exists(updateRoot))
                {
                    foreach (var existing in Directory.GetFiles(updateRoot, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(existing, FileAttributes.Normal);
                    }
                }

                if (Directory.Exists(updateRoot))
                {
                    Directory.Delete(updateRoot, true);
                }

                Directory.CreateDirectory(updateRoot);
                ZipFile.ExtractToDirectory(targetPath, updateRoot, overwriteFiles: true);
            }
            catch
            {
                ZipFile.ExtractToDirectory(targetPath, updateRoot);
            }

            var launched = await TryLaunchExtractedAsync(updateRoot);
            if (launched)
            {
                await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.Navigation.PopModalAsync());
                QuitApp();
                return;
            }

            await ShowAlertAsync(_localization["Update_Title"], _localization["Update_ManualLaunch"]);
        }
        else
        {
            await Launcher.OpenAsync(new OpenFileRequest("Update", new ReadOnlyFile(targetPath)));
        }

        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.Navigation.PopModalAsync());
    }

    private static string SanitizeFolderName(string value)
    {
        foreach (var c in PathIO.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '-');
        }

        return value.Trim();
    }

    private static void QuitApp()
    {
        try
        {
            Application.Current?.Quit();
        }
        catch
        {
            // best-effort
        }

        Environment.Exit(0);
    }

    private Task<bool> TryLaunchExtractedAsync(string folder)
    {
#if WINDOWS
        var exe = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories)
            .OrderByDescending(f => string.Equals(PathIO.GetFileName(f), "ClientTracker.exe", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(exe))
        {
            return Task.FromResult(false);
        }

        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = PathIO.GetDirectoryName(exe)
        });

        return Task.FromResult(true);
#elif MACCATALYST
        var app = Directory.GetDirectories(folder, "*.app", SearchOption.AllDirectories).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(app))
        {
            return Task.FromResult(false);
        }

        return Launcher.OpenAsync(new OpenFileRequest("Update", new ReadOnlyFile(app)));
#else
        return Task.FromResult(false);
#endif
    }

    private async Task<GitHubRelease?> GetLatestReleaseAsync(UpdateSettings settings)
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClientTracker", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (!settings.IncludePreReleases)
        {
            var latestUrl = $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepo}/releases/latest";
            var latestJson = await _httpClient.GetStringAsync(latestUrl);
            return ParseRelease(latestJson);
        }

        var releasesUrl = $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepo}/releases";
        var json = await _httpClient.GetStringAsync(releasesUrl);
        var releases = ParseReleases(json);
        return releases.FirstOrDefault();
    }

    private static GitHubRelease? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseRelease(doc.RootElement);
    }

    private static List<GitHubRelease> ParseReleases(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<GitHubRelease>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var release = ParseRelease(element);
            if (release is not null)
            {
                list.Add(release);
            }
        }

        return list;
    }

    private static GitHubRelease? ParseRelease(JsonElement element)
    {
        if (!element.TryGetProperty("tag_name", out var tagProp))
        {
            return null;
        }

        var tagName = tagProp.GetString() ?? string.Empty;
        var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        var display = string.IsNullOrWhiteSpace(name) ? tagName : name!;
        var notes = element.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
        var htmlUrl = element.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;

        var assets = new List<GitHubAsset>();
        if (element.TryGetProperty("assets", out var assetsProp))
        {
            foreach (var asset in assetsProp.EnumerateArray())
            {
                var assetName = asset.TryGetProperty("name", out var assetNameProp) ? assetNameProp.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var urlProp2) ? urlProp2.GetString() : null;
                if (!string.IsNullOrWhiteSpace(assetName) && !string.IsNullOrWhiteSpace(url))
                {
                    assets.Add(new GitHubAsset(assetName!, url!));
                }
            }
        }

        return new GitHubRelease(tagName, display, notes ?? string.Empty, htmlUrl ?? string.Empty, assets);
    }

    private static bool IsNewerVersion(string releaseTag, string currentVersion)
    {
        var releaseVersion = NormalizeVersion(releaseTag);
        var current = NormalizeVersion(currentVersion);
        if (releaseVersion is null || current is null)
        {
            return false;
        }

        return releaseVersion > current;
    }

    private static Version? NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[1..];
        }

        return Version.TryParse(cleaned, out var version) ? version : null;
    }

    private static string? SelectAssetUrl(IEnumerable<GitHubAsset> assets, UpdateSettings settings)
    {
        var patterns = GetPatterns(settings);
        foreach (var asset in assets)
        {
            if (patterns.Any(p => asset.Name.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return asset.DownloadUrl;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetPatterns(UpdateSettings settings)
    {
#if ANDROID
        var raw = settings.AndroidAssetPattern;
#elif MACCATALYST
        var raw = settings.MacAssetPattern;
#else
        var raw = settings.WindowsAssetPattern;
#endif
        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private Task ShowAlertAsync(string title, string message)
    {
        var ok = _localization["Update_Ok"];
        return MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(title, message, ok));
    }

    private Task<bool> ShowConfirmAsync(string title, string message)
    {
        var accept = _localization["Update_Download"];
        var cancel = _localization["Update_Later"];
        return MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(title, message, accept, cancel));
    }

    private Task<bool> ShowMandatoryAsync(string title, string message)
    {
        var accept = _localization["Update_Download"];
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Shell.Current.DisplayAlertAsync(title, message, accept);
            return true;
        });
    }

    private async Task<UpdateChoice> ShowUpdatePromptAsync(GitHubRelease release, bool requireUpdate)
    {
        if (requireUpdate)
        {
            await ShowMandatoryAsync(_localization["Update_Title"], BuildPrompt(release));
            return UpdateChoice.Download;
        }

        var tcs = new TaskCompletionSource<UpdateChoice>();
        var page = CreateUpdatePromptPage(release, tcs);
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.Navigation.PushModalAsync(page));
        return await tcs.Task;
    }

    private string BuildPrompt(GitHubRelease release)
    {
        var baseMessage = string.Format(_localization["Update_Available"], release.DisplayName);
        if (string.IsNullOrWhiteSpace(release.Notes))
        {
            return baseMessage;
        }

        return $"{baseMessage}\n\n{_localization["Update_Notes"]}:\n{release.Notes}";
    }

    private ContentPage CreateUpdatePromptPage(GitHubRelease release, TaskCompletionSource<UpdateChoice> tcs)
    {
        var message = BuildPrompt(release);
        var title = new Label
        {
            Text = _localization["Update_Title"],
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var messageLabel = new Label { Text = message };
        var notesView = new ScrollView
        {
            Content = new Label { Text = release.Notes, LineBreakMode = LineBreakMode.WordWrap }
        };
        notesView.IsVisible = !string.IsNullOrWhiteSpace(release.Notes);

        var downloadButton = new Button { Text = _localization["Update_Download"] };
        downloadButton.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(UpdateChoice.Download);
            await Shell.Current.Navigation.PopModalAsync();
        };

        var laterButton = new Button { Text = _localization["Update_Later"] };
        laterButton.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(UpdateChoice.Later);
            await Shell.Current.Navigation.PopModalAsync();
        };

        var viewNotesButton = new Button { Text = _localization["Update_ViewNotes"] };
        viewNotesButton.IsVisible = !string.IsNullOrWhiteSpace(release.HtmlUrl);
        viewNotesButton.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(UpdateChoice.ViewNotes);
            await Shell.Current.Navigation.PopModalAsync();
        };

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { viewNotesButton, laterButton, downloadButton }
        };

        var card = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                title,
                messageLabel,
                notesView,
                buttonRow
            }
        });

        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#66000000"),
            Content = new Grid
            {
                Padding = new Thickness(24),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                Children = { card }
            }
        };
    }

    private ContentPage CreateProgressPage(out Label statusLabel, out ProgressBar progressBar)
    {
        var localStatus = new Label
        {
            Text = _localization["Update_Downloading"],
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var localProgress = new ProgressBar { Progress = 0 };
        var percentLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            Text = string.Format(_localization["Update_Progress"], 0)
        };

        localProgress.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ProgressBar.Progress))
            {
                var percent = (int)Math.Round(localProgress.Progress * 100);
                percentLabel.Text = string.Format(_localization["Update_Progress"], percent);
            }
        };

        statusLabel = localStatus;
        progressBar = localProgress;

        var card = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children = { localStatus, localProgress, percentLabel }
        });

        return new ContentPage
        {
            BackgroundColor = Color.FromArgb("#66000000"),
            Content = new Grid
            {
                Padding = new Thickness(24),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                Children = { card }
            }
        };
    }

    private Border CreateCard(View content)
    {
        var background = GetThemeColor("SurfaceElevated", "Gray950", Colors.White);
        var stroke = GetThemeColor("BorderSubtle", "Gray600", Colors.LightGray);

        return new Border
        {
            Stroke = new SolidColorBrush(stroke),
            StrokeThickness = 1,
            BackgroundColor = background,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(20),
            Content = content
        };
    }

    private Color GetThemeColor(string lightKey, string darkKey, Color fallback)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return fallback;
        }

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var key = isDark ? darkKey : lightKey;
        if (resources.TryGetValue(key, out var value) && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    private Task UpdateProgressAsync(Label statusLabel, ProgressBar progressBar, double progress)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            statusLabel.Text = _localization["Update_Downloading"];
            progressBar.Progress = progress;
        });
    }

    private Task UpdateStatusAsync(Label statusLabel, string text)
    {
        return MainThread.InvokeOnMainThreadAsync(() => statusLabel.Text = text);
    }

    private enum UpdateChoice
    {
        Download,
        ViewNotes,
        Later
    }

    private sealed record GitHubRelease(string TagName, string DisplayName, string Notes, string HtmlUrl, List<GitHubAsset> Assets);
    private sealed record GitHubAsset(string Name, string DownloadUrl);

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
