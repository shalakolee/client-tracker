using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Microsoft.Maui.Storage;

namespace ClientTracker.Services;

public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager = new(
        "ClientTracker.Resources.Strings.AppResources",
        typeof(LocalizationResourceManager).Assembly);
    private const string CulturePreferenceKey = "ClientTracker.Language";
    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public static LocalizationResourceManager Instance { get; } = new();

    private LocalizationResourceManager()
    {
        var savedCulture = Preferences.Get(CulturePreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedCulture))
        {
            try
            {
                _culture = new CultureInfo(savedCulture);
            }
            catch (CultureNotFoundException)
            {
                _culture = CultureInfo.CurrentUICulture;
            }
        }

        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _resourceManager.GetString(key, _culture) ?? key;

    public CultureInfo CurrentCulture => _culture;

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(_culture, culture))
        {
            return;
        }

        _culture = culture;
        Preferences.Set(CulturePreferenceKey, culture.Name);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
    }
}
