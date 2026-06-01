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
  SessionInfo: SessionInfo.Model
  Journey: Journey.Model
}

type Msg =
  | GoBack
  | JoinCompleted of Result<unit, string>
  | StatusWritten
  | FocusPanel of int
  | NotesMsg of Notes.Msg
  | TodoListMsg of TodoList.Msg
  | SessionInfoMsg of SessionInfo.Msg
  | JourneyMsg of Journey.Msg
  | UpdateSession of Session.Data option

type OutMsg = LeaveSession of sessionId: string * user: string * wasStarted: bool

let init
  (client: FirebaseClient)
  (user: string)
  (avatarName: string)
  (sessionId: string)
  (sessionData: Session.Data)
  (notify: string -> unit)
  =
  let model = {
    Client = client
    SessionId = sessionId
    SessionData = sessionData
    User = user
    AvatarName = avatarName
    Status = "joining…"
    Focus = 1
    Notes = Notes.init client sessionId user
    TodoList = TodoList.init client sessionId
    SessionInfo = SessionInfo.init client sessionId user sessionData
    Journey = Journey.init client sessionId user avatarName sessionData notify
  }

  let joinCmd = Cmd.OfAsync.perform (fun () -> Firebase.Users.join client sessionId user avatarName) () JoinCompleted
  let goalPopupCmd = Cmd.ofMsg (SessionInfoMsg SessionInfo.MaybeShowGoalPopup)

  model, Cmd.batch [ joinCmd; goalPopupCmd ]

let update msg model : Model * Cmd<Msg> * OutMsg option =
  match msg with
  | GoBack ->
    let wasStarted = Session.Status.fromString model.SessionData.Status = Session.Status.Started

    let clearDriverCmd =
      match model.Journey.ActiveDriver with
      | Some driver when driver.Name = model.User -> Cmd.ofMsg (JourneyMsg(Journey.SetActiveDriver None))
      | _ -> Cmd.none

    let exitNotesInsertCmd =
      match Notes.isHoldingLock model.Notes with
      | true -> Cmd.ofMsg (NotesMsg Notes.ExitInsert)
      | false -> Cmd.none

    let exitGoalInsertCmd =
      match SessionInfo.isHoldingLock model.SessionInfo with
      | true -> Cmd.ofMsg (SessionInfoMsg SessionInfo.ExitInsert)
      | false -> Cmd.none

    model,
    Cmd.batch [ clearDriverCmd; exitNotesInsertCmd; exitGoalInsertCmd ],
    Some(LeaveSession(model.SessionId, model.User, wasStarted))
  | JoinCompleted(Ok()) ->
    let startedCmd =
      match Session.Status.fromString model.SessionData.Status with
      | Session.Status.Created ->
        Cmd.OfAsync.perform
          (fun () -> Firebase.Sessions.setStatus model.Client model.SessionId Session.Status.Started)
          ()
          (fun () -> StatusWritten)
      | _ -> Cmd.none

    { model with Status = "connected" }, startedCmd, None
  | JoinCompleted(Error e) ->
    let status = sprintf "join error: %s" e

    { model with Status = status }, [], None
  | StatusWritten -> model, [], None
  | FocusPanel n -> { model with Focus = n }, [], None
  | NotesMsg nMsg ->
    let m, cmd = Notes.update nMsg model.Notes
    { model with Notes = m }, Cmd.map NotesMsg cmd, None
  | TodoListMsg tMsg ->
    let m, cmd = TodoList.update tMsg model.TodoList
    { model with TodoList = m }, Cmd.map TodoListMsg cmd, None
  | SessionInfoMsg sMsg ->
    let m, cmd = SessionInfo.update sMsg model.SessionInfo
    { model with SessionInfo = m }, Cmd.map SessionInfoMsg cmd, None
  | JourneyMsg jMsg ->
    let m, cmd = Journey.update jMsg model.Journey
    { model with Journey = m }, Cmd.map JourneyMsg cmd, None
  | UpdateSession(Some data) ->
    let journeyM, journeyCmd = Journey.update (Journey.UpdateSession data) model.Journey
    let infoM, infoCmd = SessionInfo.update (SessionInfo.SessionDataUpdated data) model.SessionInfo

    {
      model with
          Journey = journeyM
          SessionInfo = infoM
          SessionData = data
    },
    Cmd.batch [ Cmd.map JourneyMsg journeyCmd; Cmd.map SessionInfoMsg infoCmd ],
    None
  | UpdateSession None -> model, [], None

let private subMap (wrap: 'a -> 'b) (subs: (string list * (Dispatch<'a> -> IDisposable)) list) =
  subs
  |> List.map (fun (key, start) -> key, (fun (dispatch: Dispatch<'b>) -> start (wrap >> dispatch)))

let subscriptions (model: Model) =
  (Notes.subscriptions model.Notes |> subMap NotesMsg)
  @ (TodoList.subscriptions model.TodoList |> subMap TodoListMsg)
  @ (Journey.subscriptions model.Journey |> subMap JourneyMsg)
  @ Firebase.Sessions.dataSubscription model.Client model.SessionId UpdateSession

let private outerBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (SpecialKey ConsoleKey.Backspace) (fun _ -> {
    Description = "back"
    Message = Some GoBack
  })
  KeyBinding.dynamic (SpecialKey ConsoleKey.Tab) (fun model -> {
    Description = "next panel"
    Message = Some(FocusPanel(model.Focus % 4 + 1))
  })
]

let private panelCount = 4

let private tryFocusNumber (key: ConsoleKeyInfo) =
  match key.KeyChar with
  | c when c >= '1' && c <= '9' ->
    let n = int c - int '0'

    match n <= panelCount with
    | true -> Some(FocusPanel n)
    | false -> None
  | _ -> None

let capturesInput (model: Model) =
  match model.Focus with
  | 1 -> SessionInfo.capturesInput model.SessionInfo
  | 2 -> Notes.capturesInput model.Notes
  | 3 -> TodoList.capturesInput model.TodoList
  | _ -> false

let private stageHelp (model: Model) =
  match model.Journey.Timer.State with
  | Timer.Running -> "stop"
  | Timer.Idle ->
    match model.Journey.ActiveDriver with
    | None -> "start"
    | Some _ -> "next"
  | _ -> "next"

let private canFastForward (model: Model) =
  match model.Journey.Timer.State with
  | Timer.Running -> true
  | _ -> false

let private globalKeyToMsg (model: Model) (gMsg: GlobalKeys.Msg) : Msg =
  match gMsg with
  | GlobalKeys.StageDrive ->
    match model.Journey.Timer.State with
    | Timer.Running -> JourneyMsg(Journey.TimerMsg Timer.Stop)
    | _ -> JourneyMsg Journey.SwitchDriver
  | GlobalKeys.FastForward -> JourneyMsg(Journey.TimerMsg Timer.SkipTimer)

let private isShiftTab (key: ConsoleKeyInfo) =
  key.Key = ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Shift)

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match capturesInput model with
  | true ->
    match model.Focus with
    | 1 -> SessionInfo.handleKey key model.SessionInfo |> Option.map SessionInfoMsg
    | 2 -> Notes.handleKey key model.Notes |> Option.map NotesMsg
    | 3 -> TodoList.handleKey key model.TodoList |> Option.map TodoListMsg
    | _ -> None
  | false ->
    GlobalKeys.handleKey (canFastForward model) key
    |> Option.map (globalKeyToMsg model)
    |> Option.orElseWith (fun () -> tryFocusNumber key)
    |> Option.orElseWith (fun () ->
      match isShiftTab key with
      | true -> Some(FocusPanel((model.Focus + 2) % 4 + 1))
      | false -> None)
    |> Option.orElseWith (fun () -> KeyBinding.handleKey outerBindings key model)
    |> Option.orElseWith (fun () ->
      match model.Focus with
      | 1 -> SessionInfo.handleKey key model.SessionInfo |> Option.map SessionInfoMsg
      | 2 -> Notes.handleKey key model.Notes |> Option.map NotesMsg
      | 3 -> TodoList.handleKey key model.TodoList |> Option.map TodoListMsg
      | _ -> None)

let private shiftTabHelp: IKeyMap =
  { new IKeyMap with
      member _.Help() =
        seq {
          Spectre.Tui.App.KeyBinding(Keys = ResizeArray [ KeyPress.For(Key.Tab).WithShift() ], Help = "prev panel")
        }
  }

let keyMap (model: Model) : Spectre.Tui.App.IKeyMap =
  let outer = KeyBinding.toKeyMap outerBindings model

  { new IKeyMap with
      member _.Help() =
        Seq.append (outer.Help()) (shiftTabHelp.Help())
  }

let helpKeyMaps (model: Model) : IKeyMap list = [ GlobalKeys.keyMap (stageHelp model) (canFastForward model) ]

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
    layout "info" |> withFixedSize (Some 8)
    layout "middle" |> withRatio 1
    layout "journey" |> withFixedSize (Some 7)
  |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(ctx: RenderContext) =
        let workPort = getPort ctx.Viewport workAreaLayout

        let focusStateFor n =
          match model.Focus = n with
          | true -> Focused
          | false -> Unfocused

        let sessionInfoFocusState =
          match model.Focus, SessionInfo.capturesInput model.SessionInfo with
          | 1, true -> Capturing
          | 1, false -> Focused
          | _ -> Unfocused

        ctx.Render(
          focusableBox
            model.SessionData.Title
            1
            sessionInfoFocusState
            (withPanelKeys
              (SessionInfo.widget model.SessionInfo)
              (SessionInfo.keyMap model.SessionInfo)
              (model.Focus = 1)),
          workPort "info"
        )

        ctx.Render(
          { new IWidget with
              member _.Render(ctx) =
                let topPort = getPort ctx.Viewport topRowLayout

                let notesFocusState =
                  match model.Focus, Notes.capturesInput model.Notes with
                  | 2, true -> Capturing
                  | 2, false -> Focused
                  | _ -> Unfocused

                ctx.Render(
                  focusableBox
                    "Notes"
                    2
                    notesFocusState
                    (withPanelKeys
                      (Notes.widget model.Notes (model.Focus = 2))
                      (Notes.keyMap model.Notes)
                      (model.Focus = 2)),
                  topPort "notes"
                )

                ctx.Render(
                  focusableBox
                    "Todo"
                    3
                    (focusStateFor 3)
                    (withPanelKeys
                      (TodoList.widget model.TodoList (model.Focus = 3))
                      (TodoList.keyMap model.TodoList)
                      (model.Focus = 3)),
                  topPort "todo"
                )
          },
          workPort "middle"
        )

        ctx.Render(
          focusableBox
            "Journey and Passengers"
            4
            (focusStateFor 4)
            (withPanelKeys (Journey.widget model.Journey) emptyKeyMap (model.Focus = 4)),
          workPort "journey"
        )
  }
