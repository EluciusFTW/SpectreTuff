module SessionView

open System
open Elmish
open Spectre.Tui
open Spectre.Tui.App
open Keymap
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  SessionId: string
  SessionData: Session.Data
  User: string
  Status: string
  Focus: int
  Notes: Notes.Model
  TodoList: TodoList.Model
  Timer: Timer.Model
  SessionInfo: SessionInfo.Model
  Avatar: Avatar.Model
}

type Msg =
  | GoBack
  | JoinCompleted of Result<unit, string>
  | FocusPanel of int
  | NotesMsg of Notes.Msg
  | TodoListMsg of TodoList.Msg
  | TimerMsg of Timer.Msg
  | SessionInfoMsg of SessionInfo.Msg
  | AvatarMsg of Avatar.Msg
  | UpdateSession of Session.Data
  | SetActiveDriver of string
  | SetUserMood of Mood
  | WidgetStateLoaded of Session.WidgetState option
  | UpsertConnectedUser of string * Session.UserPresence
  | RemoveConnectedUser of string
  | StateSaved

let init (user: string) (avatarName: string) (sessionId: string) (sessionData: Session.Data) = {
  SessionId = sessionId
  SessionData = sessionData
  User = user
  Status = "joining…"
  Focus = 1
  Notes = Notes.init ()
  TodoList = TodoList.init ()
  Timer = Timer.init ()
  SessionInfo = SessionInfo.init sessionId sessionData "joining…"
  Avatar = Avatar.init user avatarName sessionData
}

let update msg model =
  match msg with
  | GoBack ->
    let clearCmd =
      match model.Avatar.ActiveDriver with
      | Some driver when driver.Name = model.User -> Cmd.ofMsg (SetActiveDriver "")
      | _ -> Cmd.none

    model, clearCmd
  | JoinCompleted(Ok()) ->
    {
      model with
          Status = "connected"
          SessionInfo = SessionInfo.init model.SessionId model.SessionData "connected"
    },
    []
  | JoinCompleted(Error e) ->
    let status = sprintf "join error: %s" e

    {
      model with
          Status = status
          SessionInfo = SessionInfo.init model.SessionId model.SessionData status
    },
    []
  | FocusPanel n -> { model with Focus = n }, []
  | NotesMsg nMsg ->
    let m, cmd = Notes.update nMsg model.Notes
    { model with Notes = m }, Cmd.map NotesMsg cmd
  | TodoListMsg tMsg ->
    let m, cmd = TodoList.update tMsg model.TodoList
    { model with TodoList = m }, Cmd.map TodoListMsg cmd
  | TimerMsg Timer.SwitchDriver ->
    let users = model.Avatar.Users

    let nextUser =
      match model.Avatar.ActiveDriver with
      | None -> users |> List.tryHead
      | Some current ->
        let idx =
          users
          |> List.tryFindIndex (fun u -> u.Name = current.Name)
          |> Option.defaultValue -1

        Some users.[(idx + 1) % users.Length]

    let nextDriverName = nextUser |> Option.map (fun u -> u.Name)
    let connectedNames = users |> List.map (fun u -> u.Name)
    let avatarMap = users |> List.map (fun u -> u.Name, u.Creature) |> Map.ofList

    let driverCmd =
      match nextDriverName with
      | None -> Cmd.ofMsg (SetActiveDriver "")
      | Some u -> Cmd.ofMsg (SetActiveDriver u)

    {
      model with
          Avatar = {
            model.Avatar with
                ActiveDriver = nextUser
          }
          Timer = Timer.resetForDriver nextDriverName connectedNames avatarMap
    },
    Cmd.batch [ driverCmd; Cmd.ofMsg (TimerMsg Timer.Start) ]

  | TimerMsg tMsg ->
    let m, cmd = Timer.update tMsg model.Timer
    { model with Timer = m }, Cmd.map TimerMsg cmd
  | SessionInfoMsg sMsg ->
    let m, cmd = SessionInfo.update sMsg model.SessionInfo
    { model with SessionInfo = m }, Cmd.map SessionInfoMsg cmd
  | UpdateSession data ->
    let avatarM, avatarCmd = Avatar.update (Avatar.UpdateSession data) model.Avatar
    let connectedUsers = avatarM.Users |> List.map (fun u -> u.Name)
    let activeDriver = avatarM.ActiveDriver |> Option.map (fun u -> u.Name)
    let userAvatarMap = avatarM.Users |> List.map (fun u -> u.Name, u.Creature) |> Map.ofList
    let timerM, timerCmd = Timer.update (Timer.SessionUpdated(connectedUsers, activeDriver, userAvatarMap)) model.Timer

    {
      model with
          Avatar = avatarM
          Timer = timerM
          SessionData = data
    },
    Cmd.batch [ Cmd.map AvatarMsg avatarCmd; Cmd.map TimerMsg timerCmd ]
  | AvatarMsg Avatar.NextMood ->
    let m, cmd = Avatar.update Avatar.NextMood model.Avatar
    { model with Avatar = m }, Cmd.batch [ Cmd.map AvatarMsg cmd; Cmd.ofMsg (SetUserMood m.CurrentUser.Mood) ]

  | AvatarMsg Avatar.BecomeDriver ->
    let users = model.Avatar.Users

    let nextDriver =
      match users with
      | [] -> None
      | _ ->
        match model.Avatar.ActiveDriver with
        | None -> users |> List.tryHead
        | Some current ->
          let idx = users |> List.tryFindIndex (fun u -> u.Name = current.Name)

          match idx with
          | None -> users |> List.tryHead
          | Some i -> Some users.[(i + 1) % users.Length]

    let nextDriverName = nextDriver |> Option.map (fun u -> u.Name)
    let connectedNames = users |> List.map (fun u -> u.Name)
    let avatarMap = users |> List.map (fun u -> u.Name, u.Creature) |> Map.ofList

    let driverCmd =
      match nextDriverName with
      | None -> Cmd.ofMsg (SetActiveDriver "")
      | Some u -> Cmd.ofMsg (SetActiveDriver u)

    {
      model with
          Avatar = {
            model.Avatar with
                ActiveDriver = nextDriver
          }
          Timer = Timer.resetForDriver nextDriverName connectedNames avatarMap
    },
    Cmd.batch [ driverCmd; Cmd.ofMsg (TimerMsg Timer.Start) ]

  | AvatarMsg aMsg ->
    let m, cmd = Avatar.update aMsg model.Avatar
    { model with Avatar = m }, Cmd.map AvatarMsg cmd

  | SetActiveDriver _ -> model, []
  | SetUserMood _ -> model, []
  | WidgetStateLoaded(Some state) ->
    let notes = {
      model.Notes with
          FreetextContent = state.NotesFreetextContent
          ListItems =
            if isNull state.NotesListItems then
              []
            else
              state.NotesListItems.Values |> Seq.toList
          NoteMode =
            match state.NotesNoteMode with
            | "List" -> Notes.List
            | _ -> Notes.Freetext
    }

    let timer = {
      model.Timer with
          Remaining = TimeSpan.FromSeconds(float state.TimerRemainingSeconds)
    }

    let timerCmd =
      if state.TimerIsRunning then
        Cmd.ofMsg (TimerMsg Timer.Start)
      else
        Cmd.none

    {
      model with
          Notes = notes
          Timer = timer
    },
    timerCmd
  | WidgetStateLoaded None -> model, []
  | UpsertConnectedUser(user, presence) ->
    {
      model with
          Avatar = Avatar.upsertConnectedUser user presence model.Avatar
    },
    []
  | RemoveConnectedUser user ->
    {
      model with
          Avatar = Avatar.removeConnectedUser user model.Avatar
    },
    []
  | StateSaved -> model, []

let applyWidgetState (state: Session.WidgetState) (model: Model) =
  let notes = {
    model.Notes with
        FreetextContent = state.NotesFreetextContent
        ListItems =
          if isNull state.NotesListItems then
            []
          else
            state.NotesListItems.Values |> Seq.toList
        NoteMode =
          match state.NotesNoteMode with
          | "List" -> Notes.List
          | _ -> Notes.Freetext
  }

  let timer = {
    model.Timer with
        Remaining = TimeSpan.FromSeconds(float state.TimerRemainingSeconds)
  }

  {
    model with
        Notes = notes
        Timer = timer
  }

let shouldPersist (msg: Msg) (_model: Model) =
  match msg with
  | NotesMsg _ -> true
  | TimerMsg Timer.Start
  | TimerMsg Timer.Stop
  | TimerMsg Timer.Pause
  | TimerMsg Timer.SkipTimer
  | TimerMsg Timer.SkipPause
  | TimerMsg Timer.WorkFinished
  | TimerMsg Timer.BreakFinished -> true
  | _ -> false

let toWidgetState (model: Model) : Session.WidgetStateSave =
  let listItems =
    model.Notes.ListItems
    |> List.mapi (fun i item -> string i, item)
    |> dict
    |> System.Collections.Generic.Dictionary

  {
    NotesFreetextContent = model.Notes.FreetextContent
    NotesListItems = listItems
    NotesNoteMode =
      match model.Notes.NoteMode with
      | Notes.List -> "List"
      | _ -> "Freetext"
    TimerRemainingSeconds = int model.Timer.Remaining.TotalSeconds
    TimerIsRunning = model.Timer.State = Timer.Running
  }

let private outerBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (SpecialKey ConsoleKey.Backspace) (fun _ -> {
    Description = "back"
    Message = Some GoBack
  })
  KeyBinding.dynamic (SpecialKey ConsoleKey.Escape) (fun _ -> {
    Description = "back"
    Message = Some GoBack
  })
  KeyBinding.dynamic (SpecialKey ConsoleKey.Tab) (fun model -> {
    Description = "next panel"
    Message = Some(FocusPanel(model.Focus % 5 + 1))
  })
]

let private panelCount = 5

let private tryFocusNumber (key: ConsoleKeyInfo) =
  match key.KeyChar with
  | c when c >= '1' && c <= '9' ->
    let n = int c - int '0'

    match n <= panelCount with
    | true -> Some(FocusPanel n)
    | false -> None
  | _ -> None

let capturesInput (model: Model) =
  model.Focus = 1 && Notes.capturesInput model.Notes

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match capturesInput model with
  | true -> Notes.handleKey key model.Notes |> Option.map NotesMsg
  | false ->
    tryFocusNumber key
    |> Option.orElseWith (fun () -> KeyBinding.handleKey outerBindings key model)
    |> Option.orElseWith (fun () ->
      match model.Focus with
      | 1 -> Notes.handleKey key model.Notes |> Option.map NotesMsg
      | 2 -> TodoList.handleKey key model.TodoList |> Option.map TodoListMsg
      | 3 -> Timer.handleKey key model.Timer |> Option.map TimerMsg
      | 4 -> SessionInfo.handleKey key model.SessionInfo |> Option.map SessionInfoMsg
      | 5 -> Avatar.handleKey key model.Avatar |> Option.map AvatarMsg
      | _ -> None)

let keyMap (model: Model) : Spectre.Tui.App.IKeyMap =
  KeyBinding.toKeyMap outerBindings model

let private panelInnerLayout =
  layout "panel-inner"
  |> splitHorizontally [| layout "content"; layout "keys" |> withFixedSize (Some 1) |]

let private withPanelKeys (panelWidget: IWidget) (panelKeyMap: IKeyMap) (focused: bool) : IWidget =
  { new IWidget with
      member _.Render(ctx) =
        let port = getPort ctx.Viewport panelInnerLayout
        ctx.Render(panelWidget, port "content")

        match focused with
        | true -> ctx.Render(help [ panelKeyMap ] |> leftAligned, port "keys")
        | false -> ()
  }

let private topRowLayout =
  layout "top-row"
  |> splitVertically [| layout "notes" |> withRatio 1; layout "todo" |> withRatio 1 |]

let private workAreaLayout =
  layout "work-area"
  |> splitHorizontally [|
    layout "top" |> withRatio 2
    layout "timer" |> withRatio 1
    layout "bottom" |> withRatio 1
  |]

let private sessionLayout =
  layout "session"
  |> splitVertically [|
    layout "work-area" |> withRatio 1
    layout "users-column" |> withFixedSize (Some 28)
  |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(ctx: RenderContext) =
        let slotPort = getPort ctx.Viewport sessionLayout

        let focusStateFor n =
          match model.Focus = n with
          | true -> Focused
          | false -> Unfocused

        ctx.Render(
          { new IWidget with
              member _.Render(ctx) =
                let workPort = getPort ctx.Viewport workAreaLayout

                ctx.Render(
                  { new IWidget with
                      member _.Render(ctx) =
                        let topPort = getPort ctx.Viewport topRowLayout

                        let notesFocusState =
                          match model.Focus, Notes.capturesInput model.Notes with
                          | 1, true -> Capturing
                          | 1, false -> Focused
                          | _ -> Unfocused

                        ctx.Render(
                          focusableBox
                            "Notes"
                            1
                            notesFocusState
                            (withPanelKeys (Notes.widget model.Notes) (Notes.keyMap model.Notes) (model.Focus = 1)),
                          topPort "notes"
                        )

                        ctx.Render(
                          focusableBox
                            "Todo"
                            2
                            (focusStateFor 2)
                            (withPanelKeys
                              (TodoList.widget model.TodoList)
                              (TodoList.keyMap model.TodoList)
                              (model.Focus = 2)),
                          topPort "todo"
                        )
                  },
                  workPort "top"
                )

                ctx.Render(
                  focusableBox
                    "Timer"
                    3
                    (focusStateFor 3)
                    (withPanelKeys (Timer.widget model.Timer) (Timer.keyMap model.Timer) (model.Focus = 3)),
                  workPort "timer"
                )

                ctx.Render(
                  focusableBox
                    "Session Info"
                    4
                    (focusStateFor 4)
                    (withPanelKeys
                      (SessionInfo.widget model.SessionInfo)
                      (SessionInfo.keyMap model.SessionInfo)
                      (model.Focus = 4)),
                  workPort "bottom"
                )
          },
          slotPort "work-area"
        )

        ctx.Render(
          focusableBox
            "Users"
            5
            (focusStateFor 5)
            (withPanelKeys (Avatar.widget model.Avatar) (Avatar.keyMap model.Avatar) (model.Focus = 5)),
          slotPort "users-column"
        )
  }
