# Tech Context

## Stack
- .NET MAUI (Android target)
- C# ViewModels, XAML UI

## Build & deploy
- `dotnet build ClientTracker/ClientTracker.csproj -t:Run -f net10.0-android`
- Requires Android SDK `platform-tools` on PATH (adb)

## Repo layout
- `ClientTracker/` app code
- `docs/` and `material-components-android/` for design references
