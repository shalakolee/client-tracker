using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace ClientTracker.Services;

public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager = new(
        "ClientTracker.Resources.Strings.AppResources",
        typeof(LocalizationResourceManager).Assembly);
    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public static LocalizationResourceManager Instance { get; } = new();

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
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
    }
}
