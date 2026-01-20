# Active Context

## Current focus
- Fix dashboard totals disappearing in Portuguese (currency text binding issues).
- Keep dark mode assets correct (back icon, chevrons, overlays).
- Ensure navigation and update functionality work.

## Recent changes
- `DashboardViewModel` updated to push currency text updates onto UI thread.
- `LoadingOverlay` uses dark background in dark mode.
- Added `icon_back_dark.svg` and dark chevron assets; XAML uses AppThemeBinding.
- Reintroduced swipe-back gestures via `SwipeBackBehavior`.
- Added save buttons for client edit and sale details.
- Added add-client button on Add Sale page.

## Next steps
- Verify Portuguese totals no longer blink/disappear on dashboard.
- Investigate update/check update crash.
