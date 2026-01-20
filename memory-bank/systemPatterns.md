# System Patterns

## Architecture
- .NET MAUI app with MVVM patterns.
- ViewModels coordinate data from `DatabaseService` and other services.
- XAML pages with shared controls (TopBar, LoadingOverlay, TrendChart, etc.).

## UI patterns
- TopBar control used for page headers.
- Cards and lists use shared styles in `Resources/Styles/Styles.xaml`.
- AppThemeBinding used for light/dark styling.

## Navigation
- Shell routes (e.g., `//ClientsPage`, `//SalesPage`, `//PayCalendarPage`).
- Swipe-back behavior attached to many pages via `SwipeBackBehavior`.
