using System.Threading.Tasks;

namespace Microsoft.Maui.Controls;

public static class ShellAlertExtensions
{
    private static Page? GetFallbackPage()
    {
#if NET10_0_OR_GREATER
        return Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
#else
        return Application.Current?.MainPage;
#endif
    }

    public static Task DisplayAlertAsync(this Shell? shell, string title, string message, string cancel)
    {
        var page = shell as Page ?? GetFallbackPage();
        if (page is null)
        {
            return Task.CompletedTask;
        }

#if NET10_0_OR_GREATER
        return page.DisplayAlertAsync(title, message, cancel);
#else
        return page.DisplayAlert(title, message, cancel);
#endif
    }

    public static Task<bool> DisplayAlertAsync(this Shell? shell, string title, string message, string accept, string cancel)
    {
        var page = shell as Page ?? GetFallbackPage();
        if (page is null)
        {
            return Task.FromResult(false);
        }

#if NET10_0_OR_GREATER
        return page.DisplayAlertAsync(title, message, accept, cancel);
#else
        return page.DisplayAlert(title, message, accept, cancel);
#endif
    }
}
