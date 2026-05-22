module Session

open System.Collections.Generic

[<CLIMutable>]
type Data = {
  Goal: string
  StartedAt: int64
  Creator: string
  ActiveDriver: string
}

[<CLIMutable>]
type WidgetState = {
  NotesFreetextContent: string
  NotesListItems: Dictionary<string, string>
  NotesNoteMode: string
  TimerRemainingSeconds: int
  TimerIsRunning: bool
  ConnectedUsers: Dictionary<string, string>
}

[<CLIMutable>]
type WidgetStateSave = {
  NotesFreetextContent: string
  NotesListItems: Dictionary<string, string>
  NotesNoteMode: string
  TimerRemainingSeconds: int
  TimerIsRunning: bool
}
