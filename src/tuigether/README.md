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

<<<<<<< Updated upstream
=======
## Elmish MVU model

Elmish follows the Model-View-Update (MVU) pattern: the app state is a single immutable record, every change goes through a pure `update` function, and the result is re-rendered.

```
        ┌─────────────────────────────────────┐◀──────────────┐
        │               Msg                   │               │
        │  (InputMsg | Tick | SessionListMsg  │               │ new Msg
        │   | SessionViewMsg | ...)           │               │
        └───────────────┬─────────────────────┘               │
                        │                                     │
                        ▼                                     │
              ┌─────────────────┐     ┌───────────────┐       │
              │     update      │────▶│    Cmd        │───────┘
              │  Model → Model  │     │ (side-effects)│
              └────────┬────────┘     └───────────────┘
                       │
                       ▼
              ┌─────────────────┐
              │      view       │
              │  Model → Widget │ 
              └─────────────────┘
```

`Cmd` values are the only place side effects happen — Firebase writes, async reads, or self-scheduled ticks. They resolve to a new `Msg` that re-enters `update`.

### Message sources

Three external sources feed into the top-level `Msg` DU:

```
  Keyboard input          Firebase streams          Internal timers
  ─────────────           ───────────────           ───────────────
  Input.KeyPressed        SessionEvent              Tick (1 s)
        │                 RemoteStateLoaded         FlashTick (200 ms)
        │                 UpdateSession             BreakTick (500 ms)
        │                       │                   MaybeSaveFreetext (300 ms)
        │                       │                   MaybeAutoExitInsert (30 s)
        │                       │                         │
        └───────────────────────┴─────────────────────────┘
                                │
                      Application.update
```

### Model nesting

The model is a tree that mirrors the page/panel hierarchy. Each node owns its slice of state and exposes its own `Msg` type; the parent wraps child messages in its own DU cases.

```
Application.Model
├── page : SessionListPage | SessionViewPage
├── SessionList.Model
│   ├── sessions : (string * Session.Data) list
│   ├── connectedUsers : Map<string, Set<string>>
│   └── inputMode : Browsing | Naming
└── SessionView.Model
    ├── Notes.Model
    │   ├── noteMode : Freetext | List
    │   ├── inputMode : Normal | Insert | AddingItem
    │   └── lock : { owner; lockedAt } option
    ├── TodoList.Model
    ├── SessionInfo.Model
    └── Journey.Model
        └── Timer.Model
            ├── phase : Work | Break
            ├── state : Idle | Running | Paused | Flashing | Breaking
            └── activeDriver : string option
```

### Message routing example — a keypress reaching the timer

```
KeyPressed Space
    │
    ▼  Application.update
InputMsg(KeyPressed Space)
    │  dispatch to focused panel
    ▼  SessionView.update
SessionViewMsg(JourneyMsg(...))
    │
    ▼  Journey.update → Timer.update
TimerMsg Start
    │
    ▼  Timer.update
  Model { state = Running }  +  Cmd (write state to Firebase)
                                      │
                                      ▼ (async resolves)
                                 TimerMsg StateSaved
```

### Notes locking flow

Notes and session goals use a Firebase-backed lock record so only one writer is active at a time.

```
User presses i          User presses Esc
      │                       │
      ▼                       ▼
EnterInsert              ExitInsert
      │                       │
  write lock              clear lock
  to Firebase             in Firebase
      │                       │
  inputMode                inputMode
  = Insert                 = Normal
      │
  TypeChar / TypeBackspace
      │
  MaybeSaveFreetext       ←── debounced 300 ms
  (token-gated)
      │
  write content to Firebase
```

>>>>>>> Stashed changes
## Logs

Daily log files are written to `./logs/tuigether-YYYY-MM-DD.log` (or `TUIGETHER_LOG_DIR`). Press `l` in the app to open the inline log viewer.
