module SessionInfo

open System
open Elmish
open Firebase.Database
open Spectre.Tui
open Keymap
open SpectreTuff.Layout
open SpectreTuff.Widgets

type InputMode =
  | Normal
  | Insert

// Goal editing is single-writer: the user in Insert mode owns the lock and other
// users cannot enter Insert until it's released. LockedAt is refreshed on every
// debounced save so a crashed holder's lock expires after lockTtlMs.
type Lock = { Owner: string; LockedAt: int64 }

type Model = {
  Client: FirebaseClient
  SessionId: string
  User: string
  StartedAt: int64
  GoalContent: string
  GoalSaveToken: int
  InsertActivityToken: int
  InputMode: InputMode
  Lock: Lock option
}

type Msg =
  | EnterInsert
  | ExitInsert
  | TypeChar of char
  | TypeBackspace
  | TypeNewLine
  | MaybeSaveGoal of int
  | MaybeAutoExitInsert of int
  | SessionDataUpdated of Session.Data
  | StateSaved

let private goalDebounceMs = 300
let private autoExitInsertMs = 30_000
let private lockTtlMs = 60_000L

let private nowMs () : int64 =
  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let private isLockActive (now: int64) (lock: Lock option) =
  match lock with
  | Some l -> now - l.LockedAt <= lockTtlMs
  | None -> false

let private isLockedByOther (model: Model) =
  match model.Lock with
  | Some l when isLockActive (nowMs ()) (Some l) -> l.Owner <> model.User
  | _ -> false

let isHoldingLock (model: Model) =
  match model.InputMode, model.Lock with
  | Insert, Some l -> l.Owner = model.User
  | _ -> false

let private lockFromData (data: Session.Data) =
  match isNull data.GoalLockOwner || data.GoalLockOwner = "" with
  | true -> None
  | false ->
    Some {
      Owner = data.GoalLockOwner
      LockedAt = data.GoalLockedAt
    }

let init (client: FirebaseClient) (sessionId: string) (user: string) (sessionData: Session.Data) = {
  Client = client
  SessionId = sessionId
  User = user
  StartedAt = sessionData.StartedAt
  GoalContent =
    match isNull sessionData.Goal with
    | true -> ""
    | false -> sessionData.Goal
  GoalSaveToken = 0
  InsertActivityToken = 0
  InputMode = Normal
  Lock = lockFromData sessionData
}

let private normalBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (CharKey 'i') (fun model ->
    match isLockedByOther model with
    | true ->
      let owner =
        model.Lock
        |> Option.map (fun l -> l.Owner)
        |> Option.defaultValue "another user"

      {
        Description = sprintf "locked by %s" owner
        Message = Some EnterInsert
      }
    | false -> {
        Description = "edit goal"
        Message = Some EnterInsert
      })
]

let private insertModeBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Escape "exit insert" ExitInsert
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match model.InputMode with
  | Insert ->
    match key.Key with
    | ConsoleKey.Escape -> Some ExitInsert
    | ConsoleKey.Backspace -> Some TypeBackspace
    | ConsoleKey.Enter -> Some TypeNewLine
    | _ when key.KeyChar <> '\000' -> Some(TypeChar key.KeyChar)
    | _ -> None
  | Normal ->
    match key.KeyChar with
    | 'i' -> Some EnterInsert
    | _ -> None

let capturesInput (model: Model) =
  match model.InputMode with
  | Insert -> true
  | Normal -> false

let keyMap (model: Model) =
  let bindings =
    match model.InputMode with
    | Insert -> insertModeBindings
    | Normal -> normalBindings

  KeyBinding.toKeyMap bindings model

let private saveGoalCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Sessions.saveGoal model.Client model.SessionId model.GoalContent)
    ()
    (fun () -> StateSaved)

// Debounce goal writes: each typed character bumps a token and schedules a
// MaybeSaveGoal for that token after a short idle delay. The actual save only
// fires if the token still matches the latest one — so a burst of fast
// keystrokes collapses into a single Firebase write at the end.
let private scheduleGoalSave (token: int) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep goalDebounceMs }) () (fun () -> MaybeSaveGoal token)

// Auto-exit Insert mode after autoExitInsertMs of no typing activity. Every
// keystroke bumps the activity token and schedules a fresh check; only the
// scheduled check whose token still matches actually exits.
let private scheduleAutoExit (token: int) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep autoExitInsertMs }) () (fun () -> MaybeAutoExitInsert token)

let private saveGoalLockCmd (model: Model) : Cmd<Msg> =
  match model.Lock with
  | Some lock ->
    Cmd.OfAsync.perform
      (fun () -> Firebase.Sessions.saveGoalLock model.Client model.SessionId lock.Owner lock.LockedAt)
      ()
      (fun () -> StateSaved)
  | None -> []

let private releaseGoalLockCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> Firebase.Sessions.releaseGoalLock model.Client model.SessionId) () (fun () ->
    StateSaved)

let update msg model =
  match msg with
  | EnterInsert ->
    match isLockedByOther model with
    | true -> model, []
    | false ->
      let activityToken = model.InsertActivityToken + 1

      let lock = {
        Owner = model.User
        LockedAt = nowMs ()
      }

      let updated = {
        model with
            InputMode = Insert
            InsertActivityToken = activityToken
            Lock = Some lock
      }

      updated, Cmd.batch [ saveGoalLockCmd updated; scheduleAutoExit activityToken ]
  | ExitInsert ->
    match model.InputMode with
    | Normal -> model, []
    | Insert ->
      // Bump the token so any in-flight debounced save is cancelled, then flush
      // the current content immediately so other users see the final edit.
      let bumped = model.GoalSaveToken + 1

      let updated = {
        model with
            InputMode = Normal
            GoalSaveToken = bumped
            Lock = None
      }

      updated, Cmd.batch [ saveGoalCmd updated; releaseGoalLockCmd updated ]
  | TypeChar c ->
    let bumped = model.GoalSaveToken + 1
    let activityToken = model.InsertActivityToken + 1

    let updated = {
      model with
          GoalContent = model.GoalContent + string c
          GoalSaveToken = bumped
          InsertActivityToken = activityToken
    }

    updated, Cmd.batch [ scheduleGoalSave bumped; scheduleAutoExit activityToken ]
  | TypeBackspace ->
    let text = model.GoalContent
    let bumped = model.GoalSaveToken + 1
    let activityToken = model.InsertActivityToken + 1

    let updated = {
      model with
          GoalContent =
            match text with
            | "" -> ""
            | _ -> text.[.. text.Length - 2]
          GoalSaveToken = bumped
          InsertActivityToken = activityToken
    }

    updated, Cmd.batch [ scheduleGoalSave bumped; scheduleAutoExit activityToken ]
  | TypeNewLine ->
    let bumped = model.GoalSaveToken + 1
    let activityToken = model.InsertActivityToken + 1

    let updated = {
      model with
          GoalContent = model.GoalContent + "\n"
          GoalSaveToken = bumped
          InsertActivityToken = activityToken
    }

    updated, Cmd.batch [ scheduleGoalSave bumped; scheduleAutoExit activityToken ]
  | MaybeSaveGoal token ->
    match token = model.GoalSaveToken with
    | true ->
      // Refresh the lock timestamp on every save so the holder doesn't appear
      // stale to other clients while they're actively typing.
      let refreshedLock =
        match model.Lock with
        | Some l when l.Owner = model.User -> Some { l with LockedAt = nowMs () }
        | other -> other

      let updated = { model with Lock = refreshedLock }
      updated, Cmd.batch [ saveGoalCmd updated; saveGoalLockCmd updated ]
    | false -> model, []
  | MaybeAutoExitInsert token ->
    match model.InputMode = Insert && token = model.InsertActivityToken with
    | true -> model, Cmd.ofMsg ExitInsert
    | false -> model, []
  | SessionDataUpdated data ->
    // While the user is actively typing, ignore the goal echo from the remote —
    // applying it would clobber characters typed since the in-flight save was
    // dispatched.
    let goalContent =
      match model.InputMode with
      | Insert -> model.GoalContent
      | Normal ->
        match isNull data.Goal with
        | true -> ""
        | false -> data.Goal

    {
      model with
          StartedAt = data.StartedAt
          GoalContent = goalContent
          Lock = lockFromData data
    },
    []
  | StateSaved -> model, []

let subscriptions (_model: Model) = []

let private infoLayout =
  layout "session-info"
  |> splitHorizontally [| layout "goal"; layout "started" |> withFixedSize (Some 1) |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(ctx) =
        let port = getPort ctx.Viewport infoLayout

        let goalWidget =
          textBox model.GoalContent
          |> withMode TextBoxMode.MultiLine
          |> (match model.InputMode with
              | Insert -> focused >> withCursorAtEnd
              | Normal -> unfocused)
          :> IWidget

        ctx.Render(goalWidget, port "goal")

        let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(model.StartedAt).ToString("yyyy-MM-dd HH:mm:ss")
        ctx.Render(ofString (sprintf "  Started: %s" startedAt) :> IWidget, port "started")
  }
