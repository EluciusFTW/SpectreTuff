module Session

open System.Collections.Generic

[<CLIMutable>]
type UserPresence = { Avatar: string; Mood: string }

[<CLIMutable>]
type Data = {
  Goal: string
  StartedAt: int64
  Creator: string
  ActiveDriver: string
}

[<CLIMutable>]
type NotesState = {
  FreetextContent: string
  ListItems: Dictionary<string, string>
  NoteMode: string
}

[<CLIMutable>]
type TimerState = {
  RemainingSeconds: int
  IsRunning: bool
}
