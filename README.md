# ClientTracker

## Updates (in-app)

The app can check GitHub Releases for updates.

1. Open `Settings` → `Updates`
2. Set `GitHub owner` and `GitHub repo`
3. Leave the default asset patterns unless you changed the release file names
4. Use `Check` to test

Windows + MacCatalyst releases are published as `.zip` assets; the app downloads, extracts, launches the new build, and exits.
Android releases are published as `.apk` assets; the app downloads and opens the APK for installation.

## GitHub Releases

### Create a release build

Push a tag like:

`git tag v1.0.1`
`git push origin v1.0.1`

This triggers `.github/workflows/release.yml` to build and attach:
- `ClientTracker-win-x64-vX.Y.Z.zip`
- `ClientTracker-maccatalyst-x64-vX.Y.Z.zip`
- `ClientTracker-android-vX.Y.Z.apk`

