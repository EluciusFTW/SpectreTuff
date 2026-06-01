# SpectreTuff
_Spectre Tui for F#_ - A thin, opinionated wrapper around [Spectre.Tui](https://github.com/spectreconsole/spectre.tui)

> [!WARNING]
> This library, as well as Spectre.Tui is currently under construction and may change at any time.  

## Installing tuigether

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). The scripts build a
single-file, framework-dependent executable and copy it onto your `PATH` as `tuigether`.

**Linux / macOS:**

```bash
src/tuigether/scripts/install.sh             # installs to ~/.local/bin
src/tuigether/scripts/install.sh /custom/dir # or a custom directory
```

**Windows (PowerShell):**

```powershell
src\tuigether\scripts\install.ps1                      # installs to %LOCALAPPDATA%\Programs\tuigether
src\tuigether\scripts\install.ps1 -InstallDir C:\tools # or a custom directory
```

If the chosen directory is not already on your `PATH`, the script prints how to add it.

## tuigether environment variables

| Variable | Required | Default | Description |
| --- | --- | --- | --- |
| `FIREBASE_URL` | yes | — | Firebase Realtime Database URL the app connects to. |
| `FIREBASE_SECRET` | yes | — | Firebase auth secret. |
| `TUIGETHER_USER` | yes | — | Identifier shown to other participants in a session. |
| `TUIGETHER_AVATAR` | no | random pick | Preferred avatar name; falls back to random if unset or unknown. |
| `TUIGETHER_LOG_DIR` | no | `./logs` | Directory where daily log files are written. |
| `TUIGETHER_LOG_RETENTION_DAYS` | no | `14` | Days of log history to keep. Older files are deleted on startup. `0` keeps today only. |

## License
Copyright © Guy Buss, Daniel Muckelbauer

SpectreTuff is provided as-is under the MIT license.
See the LICENSE.md file included in the repository.
