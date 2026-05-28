# tuigether

A real-time, multi-user TUI app for collaborative mob/pair programming sessions, built with F# + Elmish and Firebase Realtime Database.

Features: shared Pomodoro timer, driver rotation, collaborative notes, todo list, session goals, presence visualization, and a break-time obstacle game.

## Running

```bash
# from the SpectreTuff repo root
dotnet run --project src/tuigether
```

## Configuration

On first run the app creates a template config file at the platform default location:

- **Linux / macOS**: `~/.config/tuigether/config.json`
- **Windows**: `%APPDATA%\tuigether\config.json`

```json
{
  "firebaseUrl": "https://your-project.firebaseio.com",
  "firebaseSecret": "your-auth-secret",
  "tuigetherUser": "your-username"
}
```

Environment variables override the config file:

| Variable | Required | Description |
|---|---|---|
| `FIREBASE_URL` | yes | Firebase Realtime Database URL |
| `FIREBASE_SECRET` | yes | Firebase auth secret |
| `TUIGETHER_USER` | yes | Username shown to other participants |
| `TUIGETHER_AVATAR` | no | Preferred avatar name (random if unset or unknown) |
| `TUIGETHER_LOG_DIR` | no | Directory for daily log files (default: `./logs`) |
| `TUIGETHER_LOG_RETENTION_DAYS` | no | Days of logs to keep; `0` = today only (default: `14`) |

## Architecture

```
  Terminal A          Terminal B          Terminal C
 ┌──────────┐        ┌──────────┐        ┌──────────┐
 │tuigether │        │tuigether │        │tuigether │
 │  client  │        │  client  │        │  client  │
 └────┬─────┘        └────┬─────┘        └────┬─────┘
      │  subscribe/write  │  subscribe/write  │
      └───────────────────┼───────────────────┘
                          │
               ┌──────────▼──────────┐
               │  Firebase Realtime  │
               │      Database       │
               │  (sessions, timer,  │
               │  notes, todos, ...)  │
               └─────────────────────┘
```

Each client subscribes to the shared session tree and pushes local changes (timer state, notes, driver, presence) in real time. A simple lock record per document (notes, goals) prevents simultaneous edits.

## Logs

Daily log files are written to `./logs/tuigether-YYYY-MM-DD.log` (or `TUIGETHER_LOG_DIR`). Press `l` in the app to open the inline log viewer.
