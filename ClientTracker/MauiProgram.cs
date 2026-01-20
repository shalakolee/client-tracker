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

#if ANDROID
			static Color GetThemeColor(Color light, Color dark)
				=> Application.Current?.RequestedTheme == AppTheme.Dark ? dark : light;

			static void ApplyAndroidInputBackground(Android.Views.View platformView, Color fill, Color stroke)
			{
				var context = platformView.Context;
				var corner = (float)context.ToPixels(14);
				var border = (int)context.ToPixels(1);

				var drawable = new Android.Graphics.Drawables.GradientDrawable();
				drawable.SetColor(fill.ToPlatform());
				drawable.SetCornerRadius(corner);
				drawable.SetStroke(border, stroke.ToPlatform());

				platformView.BackgroundTintList = null;
				platformView.Background = drawable;
			}
#endif

			EntryHandler.Mapper.AppendToMapping("ModernInputs", (handler, view) =>
			{
#if ANDROID
				handler.PlatformView.SetHighlightColor(selection.ToPlatform());
				var fill = GetThemeColor(Color.FromArgb("#FFFFFF"), Color.FromArgb("#0F172A"));
				var stroke = GetThemeColor(Color.FromArgb("#E2E8F0"), Color.FromArgb("#475569"));
				ApplyAndroidInputBackground(handler.PlatformView, fill, stroke);
#elif IOS || MACCATALYST
				handler.PlatformView.TintColor = accent.ToPlatform();
#elif WINDOWS
				var selectionColor = selection.ToWindowsColor();
				handler.PlatformView.SelectionHighlightColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
				handler.PlatformView.SelectionHighlightColorWhenNotFocused = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
#endif
			});

			EditorHandler.Mapper.AppendToMapping("ModernInputs", (handler, view) =>
			{
#if ANDROID
				handler.PlatformView.SetHighlightColor(selection.ToPlatform());
				var fill = GetThemeColor(Color.FromArgb("#FFFFFF"), Color.FromArgb("#0F172A"));
				var stroke = GetThemeColor(Color.FromArgb("#E2E8F0"), Color.FromArgb("#475569"));
				ApplyAndroidInputBackground(handler.PlatformView, fill, stroke);
#elif IOS || MACCATALYST
				handler.PlatformView.TintColor = accent.ToPlatform();
#elif WINDOWS
				var selectionColor = selection.ToWindowsColor();
				handler.PlatformView.SelectionHighlightColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
				handler.PlatformView.SelectionHighlightColorWhenNotFocused = new Microsoft.UI.Xaml.Media.SolidColorBrush(selectionColor);
#endif
			});

			PickerHandler.Mapper.AppendToMapping("ModernInputs", (handler, view) =>
			{
#if ANDROID
				var fill = GetThemeColor(Color.FromArgb("#FFFFFF"), Color.FromArgb("#0F172A"));
				var stroke = GetThemeColor(Color.FromArgb("#E2E8F0"), Color.FromArgb("#475569"));
				ApplyAndroidInputBackground(handler.PlatformView, fill, stroke);
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
		builder.Services.AddTransient<CommissionPlansViewModel>();
		builder.Services.AddTransient<CommissionPlanEditViewModel>();
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
		builder.Services.AddTransient<CommissionPlansPage>();
		builder.Services.AddTransient<CommissionPlanEditPage>();
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
