module Session

open System.Collections.Generic

[<CLIMutable>]
type UserPresence = { Avatar: string; Mood: string }

[<RequireQualifiedAccess>]
type Status =
  | Created
  | Started
  | Finished

module Status =
  let toString =
    function
    | Status.Created -> "Created"
    | Status.Started -> "Started"
    | Status.Finished -> "Finished"

  let fromString (s: string) =
    match s with
    | "Started" -> Status.Started
    | "Finished" -> Status.Finished
    | _ -> Status.Created

[<CLIMutable>]
type Data = {
  Goal: string
  StartedAt: int64
  WorkStartedAt: int64
  Creator: string
  ActiveDriver: string
  Status: string
}

[<CLIMutable>]
type NotesState = {
  FreetextContent: string
  ListItems: Dictionary<string, string>
  NoteMode: string
  LockOwner: string
  LockedAt: int64
}

[<CLIMutable>]
type TimerState = {
  RemainingSeconds: int
  IsRunning: bool
}

[<CLIMutable>]
type TodoItemState = { Text: string; Completed: bool }

[<CLIMutable>]
type TodoState = {
  Items: Dictionary<string, TodoItemState>
}
