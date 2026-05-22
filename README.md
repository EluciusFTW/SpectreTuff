# SpectreTuff
_Spectre Tui for F#_ - A thin, opinionated wrapper around [Spectre.Tui](https://github.com/spectreconsole/spectre.tui)

> [!WARNING]
> This library, as well as Spectre.Tui is currently under construction and may change at any time.  

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
