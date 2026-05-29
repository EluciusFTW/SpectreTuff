module SessionInfo

open System
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

type BranchPopupStage =
  | EditingName of error: string option
  | Submitting
  | CreateFailed of error: string

type BranchPopup = {
  Name: string
  Stage: BranchPopupStage
}

type SyncPopupStage =
  | RunningSync
  | SyncFailed of error: string

type InputMode =
  | Normal
  | Insert
  | GoalPopup
  | BranchPopup of BranchPopup
  | SyncPopup of SyncPopupStage

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
  GitBranch: string
  LocalGitBranch: string
  GitRepo: string
  LocalRepo: string
  SessionTitle: string
  LastSeenWipAt: int64
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
  | BeginCreateBranch
  | BranchTypeChar of char
  | BranchTypeBackspace
  | ConfirmBranch
  | DismissBranchPopup
  | BranchCreateCompleted of name: string * Result<unit, string>
  | BeginSync
  | BeginWipSync
  | SyncCompleted of Result<unit, string>
  | WipSyncCompleted of Result<unit, string>
  | DismissSyncPopup
  | MaybeShowGoalPopup
  | CloseGoalPopup

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
  | Insert, Some l
  | GoalPopup, Some l -> l.Owner = model.User
  | _ -> false

let private branchFromData (data: Session.Data) =
  match isNull data.GitBranch || data.GitBranch = "" with
  | true -> "(unknown)"
  | false -> data.GitBranch

let private repoFromData (data: Session.Data) =
  match isNull data.GitRepo with
  | true -> ""
  | false -> data.GitRepo

let private isRepoOK (model: Model) =
  match model.LocalRepo, model.GitRepo with
  | "", _ -> false
  | _, "" -> true
  | local, session -> local = session

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
  GitBranch = branchFromData sessionData
  LocalGitBranch = Git.readCurrentBranch ()
  GitRepo = repoFromData sessionData
  LocalRepo = Git.readRepoName ()
  SessionTitle =
    match isNull sessionData.Title with
    | true -> ""
    | false -> sessionData.Title
  LastSeenWipAt = sessionData.LastWipPushAt
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
  KeyBinding.dynamic (CharKey 'b') (fun model ->
    match isRepoOK model with
    | true -> {
        Description = "new branch"
        Message = Some BeginCreateBranch
      }
    | false -> {
        Description = "new branch (wrong repo)"
        Message = None
      })
  KeyBinding.dynamic (CharKey 'S') (fun model ->
    match isRepoOK model with
    | false -> {
        Description = "sync branch (wrong repo)"
        Message = None
      }
    | true ->
      let help =
        match model.LocalGitBranch = model.GitBranch with
        | true -> "pull"
        | false -> "sync branch"

      {
        Description = help
        Message = Some BeginSync
      })
  KeyBinding.dynamic (CharKey 'w') (fun model ->
    match isRepoOK model && model.LocalGitBranch = model.GitBranch with
    | true -> {
        Description = "WIP sync"
        Message = Some BeginWipSync
      }
    | false -> {
        Description = "WIP sync (unavailable)"
        Message = None
      })
]

let private insertModeBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Escape "exit insert" ExitInsert
]

let private branchEditBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Enter "create" ConfirmBranch
  KeyBinding.createSpecial ConsoleKey.Escape "cancel" DismissBranchPopup
]

let private branchFailedBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Enter "retry" ConfirmBranch
  KeyBinding.createSpecial ConsoleKey.Escape "dismiss" DismissBranchPopup
]

let private syncFailedBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Escape "dismiss" DismissSyncPopup
]

let private goalPopupBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Enter "save & close" CloseGoalPopup
  KeyBinding.createSpecial ConsoleKey.Escape "dismiss" CloseGoalPopup
]

let private emptyBindings: KeyBinding<Model, Msg> list = []

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
    | 'b' when isRepoOK model -> Some BeginCreateBranch
    | 'S' when isRepoOK model -> Some BeginSync
    | 'w' when isRepoOK model && model.LocalGitBranch = model.GitBranch -> Some BeginWipSync
    | _ -> None
  | GoalPopup ->
    match key.Key with
    | ConsoleKey.Escape -> Some CloseGoalPopup
    | ConsoleKey.Enter -> Some CloseGoalPopup
    | ConsoleKey.Backspace -> Some TypeBackspace
    | _ when key.KeyChar <> '\000' -> Some(TypeChar key.KeyChar)
    | _ -> None
  | BranchPopup { Stage = EditingName _ } ->
    match key.Key with
    | ConsoleKey.Escape -> Some DismissBranchPopup
    | ConsoleKey.Enter -> Some ConfirmBranch
    | ConsoleKey.Backspace -> Some BranchTypeBackspace
    | _ when key.KeyChar <> '\000' -> Some(BranchTypeChar key.KeyChar)
    | _ -> None
  | BranchPopup { Stage = Submitting } -> None
  | BranchPopup { Stage = CreateFailed _ } ->
    match key.Key with
    | ConsoleKey.Enter -> Some ConfirmBranch
    | ConsoleKey.Escape -> Some DismissBranchPopup
    | _ -> None
  | SyncPopup RunningSync -> None
  | SyncPopup(SyncFailed _) ->
    match key.Key with
    | ConsoleKey.Enter
    | ConsoleKey.Escape -> Some DismissSyncPopup
    | _ -> None

let capturesInput (model: Model) =
  match model.InputMode with
  | Normal -> false
  | _ -> true

let keyMap (model: Model) =
  let bindings =
    match model.InputMode with
    | Normal -> normalBindings
    | Insert -> insertModeBindings
    | GoalPopup -> goalPopupBindings
    | BranchPopup { Stage = EditingName _ } -> branchEditBindings
    | BranchPopup { Stage = Submitting } -> emptyBindings
    | BranchPopup { Stage = CreateFailed _ } -> branchFailedBindings
    | SyncPopup RunningSync -> emptyBindings
    | SyncPopup(SyncFailed _) -> syncFailedBindings

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

let private createBranchCmd (name: string) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> Git.createAndPushBranch name) () (fun result -> BranchCreateCompleted(name, result))

let private syncCmd (onSessionBranch: bool) (sessionBranch: string) : Cmd<Msg> =
  let work =
    match onSessionBranch with
    | true -> Git.syncCurrentBranch ()
    | false -> Git.fetchAndCheckout sessionBranch

  Cmd.OfAsync.perform (fun () -> work) () SyncCompleted

let private wipSyncCmd (title: string) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> Git.wipSync title) () WipSyncCompleted

let private saveWipPushCmd (model: Model) (timestamp: int64) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Sessions.saveWipPush model.Client model.SessionId model.User timestamp)
    ()
    (fun () -> StateSaved)

let private saveGitBranchCmd (model: Model) (branch: string) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> Firebase.Sessions.saveGitBranch model.Client model.SessionId branch) () (fun () ->
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
    | _ -> model, []
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
      | Insert
      | GoalPopup -> model.GoalContent
      | _ ->
        match isNull data.Goal with
        | true -> ""
        | false -> data.Goal

    let newGitBranch = branchFromData data
    let newGitRepo = repoFromData data

    let repoOK =
      match model.LocalRepo, newGitRepo with
      | "", _ -> false
      | _, "" -> true
      | local, session -> local = session

    let isRemoteWipPush =
      data.LastWipPushAt > model.LastSeenWipAt
      && not (isNull data.LastWipPushBy)
      && data.LastWipPushBy <> model.User

    let shouldAutoPull =
      isRemoteWipPush
      && model.InputMode = Normal
      && repoOK
      && model.LocalGitBranch = newGitBranch

    let updated = {
      model with
          StartedAt = data.StartedAt
          GoalContent = goalContent
          Lock = lockFromData data
          GitBranch = newGitBranch
          GitRepo = newGitRepo
          SessionTitle =
            match isNull data.Title with
            | true -> ""
            | false -> data.Title
          LastSeenWipAt = data.LastWipPushAt
          InputMode =
            match shouldAutoPull with
            | true -> SyncPopup RunningSync
            | false -> model.InputMode
    }

    let cmd =
      match shouldAutoPull with
      | true -> syncCmd true newGitBranch
      | false -> []

    updated, cmd
  | StateSaved -> model, []
  | BeginCreateBranch ->
    match model.InputMode, isRepoOK model with
    | Normal, true ->
      {
        model with
            InputMode = BranchPopup { Name = ""; Stage = EditingName None }
      },
      []
    | _ -> model, []
  | BranchTypeChar c ->
    match model.InputMode with
    | BranchPopup({ Stage = EditingName _ } as popup) ->
      {
        model with
            InputMode =
              BranchPopup {
                popup with
                    Name = popup.Name + string c
                    Stage = EditingName None
              }
      },
      []
    | _ -> model, []
  | BranchTypeBackspace ->
    match model.InputMode with
    | BranchPopup({ Stage = EditingName _ } as popup) ->
      let trimmed =
        match popup.Name with
        | "" -> ""
        | text -> text.[.. text.Length - 2]

      {
        model with
            InputMode =
              BranchPopup {
                popup with
                    Name = trimmed
                    Stage = EditingName None
              }
      },
      []
    | _ -> model, []
  | ConfirmBranch ->
    match model.InputMode with
    | BranchPopup popup ->
      let trimmed = popup.Name.Trim()

      match trimmed with
      | "" ->
        {
          model with
              InputMode =
                BranchPopup {
                  popup with
                      Stage = EditingName(Some "Name required")
                }
        },
        []
      | _ ->
        {
          model with
              InputMode = BranchPopup { Name = trimmed; Stage = Submitting }
        },
        createBranchCmd trimmed
    | _ -> model, []
  | DismissBranchPopup ->
    match model.InputMode with
    | BranchPopup _ -> { model with InputMode = Normal }, []
    | _ -> model, []
  | BranchCreateCompleted(name, Ok()) ->
    let updated = {
      model with
          InputMode = Normal
          GitBranch = name
          LocalGitBranch = Git.readCurrentBranch ()
    }

    updated, saveGitBranchCmd updated name
  | BranchCreateCompleted(name, Error err) ->
    {
      model with
          InputMode =
            BranchPopup {
              Name = name
              Stage = CreateFailed err
            }
          LocalGitBranch = Git.readCurrentBranch ()
    },
    []
  | BeginSync ->
    match model.InputMode, isRepoOK model with
    | Normal, true ->
      let onSessionBranch = model.LocalGitBranch = model.GitBranch

      {
        model with
            InputMode = SyncPopup RunningSync
      },
      syncCmd onSessionBranch model.GitBranch
    | _ -> model, []
  | BeginWipSync ->
    match model.InputMode, isRepoOK model && model.LocalGitBranch = model.GitBranch with
    | Normal, true ->
      {
        model with
            InputMode = SyncPopup RunningSync
      },
      wipSyncCmd model.SessionTitle
    | _ -> model, []
  | SyncCompleted(Ok()) ->
    {
      model with
          InputMode = Normal
          LocalGitBranch = Git.readCurrentBranch ()
    },
    []
  | SyncCompleted(Error err) ->
    {
      model with
          InputMode = SyncPopup(SyncFailed err)
          LocalGitBranch = Git.readCurrentBranch ()
    },
    []
  | WipSyncCompleted(Ok()) ->
    let timestamp = nowMs ()

    let updated = {
      model with
          InputMode = Normal
          LocalGitBranch = Git.readCurrentBranch ()
          LastSeenWipAt = timestamp
    }

    updated, saveWipPushCmd updated timestamp
  | WipSyncCompleted(Error err) ->
    {
      model with
          InputMode = SyncPopup(SyncFailed err)
          LocalGitBranch = Git.readCurrentBranch ()
    },
    []
  | DismissSyncPopup ->
    match model.InputMode with
    | SyncPopup _ -> { model with InputMode = Normal }, []
    | _ -> model, []
  | MaybeShowGoalPopup ->
    match model.InputMode, model.GoalContent.Trim() = "", isLockedByOther model with
    | Normal, true, false ->
      let lock = {
        Owner = model.User
        LockedAt = nowMs ()
      }

      let updated = {
        model with
            InputMode = GoalPopup
            Lock = Some lock
      }

      updated, saveGoalLockCmd updated
    | _ -> model, []
  | CloseGoalPopup ->
    match model.InputMode with
    | GoalPopup ->
      let bumped = model.GoalSaveToken + 1

      let updated = {
        model with
            InputMode = Normal
            GoalSaveToken = bumped
            Lock = None
      }

      updated, Cmd.batch [ saveGoalCmd updated; releaseGoalLockCmd updated ]
    | _ -> model, []

let subscriptions (_model: Model) = []

let private goalLook = Look.fromColor Color.Yellow |> Look.withDecorations [ Decoration.Italic ]

let private infoLayout =
  layout "session-info"
  |> splitHorizontally [|
    layout "goal"
    layout "repo" |> withFixedSize (Some 1)
    layout "branch" |> withFixedSize (Some 1)
    layout "started" |> withFixedSize (Some 1)
  |]

let private popupInnerLayout =
  layout "popup-inner"
  |> splitHorizontally [| layout "input" |> withFixedSize (Some 1); layout "status" |]

let private renderBranchPopup (popup: BranchPopup) : IWidget =
  let inputWidget: IWidget =
    match popup.Stage with
    | EditingName _ ->
      textBox popup.Name
      |> withMode TextBoxMode.SingleLine
      |> withPlaceholder "branch name…"
      |> focused
      |> withCursorAtEnd
      :> IWidget
    | Submitting -> ofString (sprintf "  Creating %s…" popup.Name) :> IWidget
    | CreateFailed _ -> ofString (sprintf "  %s" popup.Name) :> IWidget

  let statusWidget: IWidget =
    match popup.Stage with
    | EditingName(Some err) -> paragraph [ Text.line [ Text.styledSpan (Nullable(Style Color.Red)) err ] ] :> IWidget
    | EditingName None -> ofString "" :> IWidget
    | Submitting -> ofString "" :> IWidget
    | CreateFailed err -> paragraph [ Text.line [ Text.styledSpan (Nullable(Style Color.Red)) err ] ] :> IWidget

  let inner =
    { new IWidget with
        member _.Render(innerCtx) =
          let port = getPort innerCtx.Viewport popupInnerLayout
          innerCtx.Render(inputWidget, port "input")
          innerCtx.Render(statusWidget, port "status")
    }

  box (Look.fromColor Color.Green)
  |> withTitle "New branch"
  |> withInnerWidget inner
  :> IWidget

let private renderGoalPopup (goalContent: string) : IWidget =
  let inputWidget: IWidget =
    textBox goalContent
    |> withMode TextBoxMode.SingleLine
    |> withPlaceholder "what are you working on?"
    |> focused
    |> withCursorAtEnd
    :> IWidget

  let inner =
    { new IWidget with
        member _.Render(innerCtx) =
          let port = getPort innerCtx.Viewport popupInnerLayout
          innerCtx.Render(inputWidget, port "input")
          innerCtx.Render(ofString "" :> IWidget, port "status")
    }

  box (Look.fromColor Color.Green)
  |> withTitle "Session goal"
  |> withInnerWidget inner
  :> IWidget

let private renderSyncPopup (stage: SyncPopupStage) (target: string) : IWidget =
  let body: IWidget =
    match stage with
    | RunningSync -> ofString (sprintf "  Syncing %s…" target) :> IWidget
    | SyncFailed err -> paragraph [ Text.line [ Text.styledSpan (Nullable(Style Color.Red)) err ] ] :> IWidget

  box (Look.fromColor Color.Green)
  |> withTitle "Sync branch"
  |> withInnerWidget body
  :> IWidget

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(ctx) =
        let port = getPort ctx.Viewport infoLayout

        let goalWidget =
          textBox model.GoalContent
          |> withMode TextBoxMode.MultiLine
          |> TextBoxes.withLook goalLook
          |> (match model.InputMode with
              | Insert -> focused >> withCursorAtEnd
              | _ -> unfocused)
          :> IWidget

        ctx.Render(goalWidget, View.padWith (View.padding 1 0 0 0) (port "goal"))

        let sessionRepoDisplay =
          match model.GitRepo with
          | "" -> "(unknown)"
          | name -> name

        let repoLine =
          match model.LocalRepo, model.GitRepo with
          | "", _ -> sprintf "  Repo:    %s [red](NO REPOSITORY)[/]" sessionRepoDisplay
          | _, "" -> sprintf "  Repo:    %s" sessionRepoDisplay
          | local, session when local = session -> sprintf "  Repo:    %s" sessionRepoDisplay
          | local, _ -> sprintf "  Repo:    %s [red](%s)[/]" sessionRepoDisplay local

        ctx.Render(ofMarkup repoLine :> IWidget, port "repo")

        let branchLine =
          match isRepoOK model with
          | false -> ""
          | true ->
            match model.LocalGitBranch = "" || model.LocalGitBranch = model.GitBranch with
            | true -> sprintf "  Branch:  %s" model.GitBranch
            | false -> sprintf "  Branch:  %s [red](%s)[/]" model.GitBranch model.LocalGitBranch

        ctx.Render(ofMarkup branchLine :> IWidget, port "branch")

        let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(model.StartedAt).ToString("yyyy-MM-dd HH:mm:ss")
        ctx.Render(ofString (sprintf "  Started: %s" startedAt) :> IWidget, port "started")

        match model.InputMode with
        | BranchPopup branchState ->
          ctx.Render(popup 60 6 |> withPopupContent (renderBranchPopup branchState) :> IWidget)
        | SyncPopup stage ->
          ctx.Render(popup 60 5 |> withPopupContent (renderSyncPopup stage model.GitBranch) :> IWidget)
        | GoalPopup -> ctx.Render(popup 60 5 |> withPopupContent (renderGoalPopup model.GoalContent) :> IWidget)
        | Normal
        | Insert -> ()
  }
