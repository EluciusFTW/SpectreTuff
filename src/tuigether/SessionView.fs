module SessionView

open System
open Elmish
open Firebase.Database
open Spectre.Tui
open Spectre.Tui.App
open Keymap
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  Client: FirebaseClient
  SessionId: string
  SessionData: Session.Data
  User: string
  AvatarName: string
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
  | LeaveCompleted of Result<unit, string>
  | FocusPanel of int
  | NotesMsg of Notes.Msg
  | TodoListMsg of TodoList.Msg
  | TimerMsg of Timer.Msg
  | SessionInfoMsg of SessionInfo.Msg
  | AvatarMsg of Avatar.Msg
  | UpdateSession of Session.Data option

type OutMsg = LeaveSession

let init (client: FirebaseClient) (user: string) (avatarName: string) (sessionId: string) (sessionData: Session.Data) =
  let model = {
    Client = client
    SessionId = sessionId
    SessionData = sessionData
    User = user
    AvatarName = avatarName
    Status = "joining…"
    Focus = 1
    Notes = Notes.init client sessionId
    TodoList = TodoList.init ()
    Timer = Timer.init client sessionId
    SessionInfo = SessionInfo.init sessionId sessionData "joining…"
    Avatar = Avatar.init client sessionId user avatarName sessionData
  }

  let joinCmd = Cmd.OfAsync.perform (fun () -> Firebase.Users.join client sessionId user avatarName) () JoinCompleted

  model, joinCmd

let update msg model : Model * Cmd<Msg> * OutMsg option =
  match msg with
  | GoBack ->
    let leaveCmd =
      Cmd.OfAsync.perform (fun () -> Firebase.Users.leave model.Client model.SessionId model.User) () LeaveCompleted

    let clearDriverCmd =
      match model.Avatar.ActiveDriver with
      | Some driver when driver.Name = model.User -> Cmd.ofMsg (AvatarMsg(Avatar.SetActiveDriver None))
      | _ -> Cmd.none

    model, Cmd.batch [ leaveCmd; clearDriverCmd ], Some LeaveSession
  | JoinCompleted(Ok()) ->
    {
      model with
          Status = "connected"
          SessionInfo = SessionInfo.init model.SessionId model.SessionData "connected"
    },
    [],
    None
  | JoinCompleted(Error e) ->
    let status = sprintf "join error: %s" e

    {
      model with
          Status = status
          SessionInfo = SessionInfo.init model.SessionId model.SessionData status
    },
    [],
    None
  | LeaveCompleted _ -> model, [], None
  | FocusPanel n -> { model with Focus = n }, [], None
  | NotesMsg nMsg ->
    let m, cmd = Notes.update nMsg model.Notes
    { model with Notes = m }, Cmd.map NotesMsg cmd, None
  | TodoListMsg tMsg ->
    let m, cmd = TodoList.update tMsg model.TodoList
    { model with TodoList = m }, Cmd.map TodoListMsg cmd, None
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

    let driverCmd = Cmd.ofMsg (AvatarMsg(Avatar.SetActiveDriver nextDriverName))

    {
      model with
          Avatar = {
            model.Avatar with
                ActiveDriver = nextUser
          }
          Timer = Timer.resetForDriver model.Timer nextDriverName connectedNames avatarMap
    },
    Cmd.batch [ driverCmd; Cmd.ofMsg (TimerMsg Timer.Start) ],
    None
  | TimerMsg tMsg ->
    let m, cmd = Timer.update tMsg model.Timer
    { model with Timer = m }, Cmd.map TimerMsg cmd, None
  | SessionInfoMsg sMsg ->
    let m, cmd = SessionInfo.update sMsg model.SessionInfo
    { model with SessionInfo = m }, Cmd.map SessionInfoMsg cmd, None
  | AvatarMsg aMsg ->
    let m, cmd = Avatar.update aMsg model.Avatar
    { model with Avatar = m }, Cmd.map AvatarMsg cmd, None
  | UpdateSession(Some data) ->
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
    Cmd.batch [ Cmd.map AvatarMsg avatarCmd; Cmd.map TimerMsg timerCmd ],
    None
  | UpdateSession None -> model, [], None

let private subMap (wrap: 'a -> 'b) (subs: (string list * (Dispatch<'a> -> IDisposable)) list) =
  subs
  |> List.map (fun (key, start) -> key, (fun (dispatch: Dispatch<'b>) -> start (wrap >> dispatch)))

let subscriptions (model: Model) =
  (Notes.subscriptions model.Notes |> subMap NotesMsg)
  @ (Timer.subscriptions model.Timer |> subMap TimerMsg)
  @ (Avatar.subscriptions model.Avatar |> subMap AvatarMsg)
  @ Firebase.Sessions.dataSubscription model.Client model.SessionId UpdateSession

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

let private globalKeyToMsg (gMsg: GlobalKeys.Msg) : Msg =
  match gMsg with
  | GlobalKeys.PauseDrive -> TimerMsg Timer.Pause
  | GlobalKeys.ResumeDrive -> TimerMsg Timer.Start
  | GlobalKeys.Teleport -> TimerMsg Timer.SkipTimer
  | GlobalKeys.NextDrive -> TimerMsg Timer.SwitchDriver

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match capturesInput model with
  | true -> Notes.handleKey key model.Notes |> Option.map NotesMsg
  | false ->
    GlobalKeys.handleKey key
    |> Option.map globalKeyToMsg
    |> Option.orElseWith (fun () -> tryFocusNumber key)
    |> Option.orElseWith (fun () -> KeyBinding.handleKey outerBindings key model)
    |> Option.orElseWith (fun () ->
      match model.Focus with
      | 1 -> Notes.handleKey key model.Notes |> Option.map NotesMsg
      | 2 -> TodoList.handleKey key model.TodoList |> Option.map TodoListMsg
      | 4 -> SessionInfo.handleKey key model.SessionInfo |> Option.map SessionInfoMsg
      | 5 -> Avatar.handleKey key model.Avatar |> Option.map AvatarMsg
      | _ -> None)
    |> Option.orElseWith (fun () -> Timer.handleKey key model.Timer |> Option.map TimerMsg)

let keyMap (model: Model) : Spectre.Tui.App.IKeyMap =
  KeyBinding.toKeyMap outerBindings model

let helpKeyMaps: IKeyMap list = [ GlobalKeys.keyMap ]

let private emptyKeyMap: IKeyMap =
  { new IKeyMap with
      member _.Help() =
        Seq.empty
  }

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
                    (withPanelKeys (Timer.widget model.Timer) emptyKeyMap (model.Focus = 3)),
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
