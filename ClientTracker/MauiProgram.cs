using ClientTracker.Pages;
using ClientTracker.Services;
using ClientTracker.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace ClientTracker;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.ConfigureMauiHandlers(handlers =>
		{
			var accent = Color.FromArgb("#1D4ED8");
			var selection = Color.FromArgb("#93C5FD");

			EntryHandler.Mapper.AppendToMapping("BlueTheme", (handler, view) =>
			{
#if ANDROID
				handler.PlatformView.SetHighlightColor(selection.ToPlatform());
				handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(accent.ToPlatform());
#elif IOS || MACCATALYST
				handler.PlatformView.TintColor = accent.ToPlatform();
#elif WINDOWS
				var selectionColor = selection.ToWindowsColor();
				handler.PlatformView.SelectionHighlightColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
				handler.PlatformView.SelectionHighlightColorWhenNotFocused = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
#endif
			});

			EditorHandler.Mapper.AppendToMapping("BlueTheme", (handler, view) =>
			{
#if ANDROID
				handler.PlatformView.SetHighlightColor(selection.ToPlatform());
				handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(accent.ToPlatform());
#elif IOS || MACCATALYST
				handler.PlatformView.TintColor = accent.ToPlatform();
#elif WINDOWS
				var selectionColor = selection.ToWindowsColor();
				handler.PlatformView.SelectionHighlightColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
				handler.PlatformView.SelectionHighlightColorWhenNotFocused = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
#endif
			});

			PickerHandler.Mapper.AppendToMapping("BlueTheme", (handler, view) =>
			{
#if ANDROID
				handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(accent.ToPlatform());
#elif IOS || MACCATALYST
				handler.PlatformView.TintColor = accent.ToPlatform();
#endif
			});
		});

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<LocalizationResourceManager>(_ => LocalizationResourceManager.Instance);
        builder.Services.AddSingleton(new HttpClient());
        builder.Services.AddSingleton<UpdateService>();
        builder.Services.AddTransient<LocalizationKeyConverter>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ClientsViewModel>();
		builder.Services.AddTransient<SalesViewModel>();
		builder.Services.AddTransient<CalendarViewModel>();
		builder.Services.AddTransient<ContactsViewModel>();
		builder.Services.AddTransient<ContactEditViewModel>();
		builder.Services.AddTransient<ContactDetailsViewModel>();
		builder.Services.AddTransient<ClientViewViewModel>();
		builder.Services.AddTransient<ClientEditViewModel>();
		builder.Services.AddTransient<SaleDetailsViewModel>();
		builder.Services.AddTransient<AddSaleViewModel>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<ClientsPage>();
		builder.Services.AddTransient<SalesPage>();
		builder.Services.AddTransient<PayCalendarPage>();
		builder.Services.AddTransient<ContactsPage>();
		builder.Services.AddTransient<ContactDetailsPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<ClientViewPage>();
		builder.Services.AddTransient<ClientEditPage>();
		builder.Services.AddTransient<SaleDetailsPage>();
		builder.Services.AddTransient<AddSalePage>();
		builder.Services.AddTransient<ContactEditPage>();
		builder.Services.AddTransient<SettingsViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
