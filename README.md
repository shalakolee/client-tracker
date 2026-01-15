# ClientTracker

## Updates (in-app)

The app checks GitHub Releases for updates (repo is built-in) and will download/install automatically when a newer release is published.

- Windows: downloads the `.msix` and launches the installer.
- MacCatalyst: downloads the `.dmg` and launches it.
- Android: downloads the `.apk` and opens it for installation (platform limitation: installs cannot be silent).

## Data

- Local dev DB: `ClientTracker/data/client-tracker.db3` (SQLite)
- MySQL migration guide: `docs/aws-mysql-migration.md`

## GitHub Releases

### Create a release build

Create/publish a GitHub Release. This triggers `.github/workflows/release-build.yml` and attaches:
- `ClientTracker-windows.msix`
- `ClientTracker-maccatalyst.dmg`
- `ClientTracker-android.apk`
