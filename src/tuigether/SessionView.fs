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

let init (user: string) (sessionId: string) (sessionData: Session.Data) = {
  SessionId = sessionId
  SessionData = sessionData
  User = user
  Status = "joining…"
  Focus = 1
  Notes = Notes.init ()
  TodoList = TodoList.init ()
  Timer = Timer.init ()
  SessionInfo = SessionInfo.init sessionId sessionData "joining…"
  Avatar = Avatar.init sessionData
}

let update msg model =
  match msg with
  | GoBack -> model, []
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
  | TimerMsg tMsg ->
    let m, cmd = Timer.update tMsg model.Timer
    { model with Timer = m }, Cmd.map TimerMsg cmd
  | SessionInfoMsg sMsg ->
    let m, cmd = SessionInfo.update sMsg model.SessionInfo
    { model with SessionInfo = m }, Cmd.map SessionInfoMsg cmd
  | AvatarMsg aMsg ->
    let m, cmd = Avatar.update aMsg model.Avatar
    { model with Avatar = m }, Cmd.map AvatarMsg cmd

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
    if n <= panelCount then Some(FocusPanel n) else None
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
  |> splitVertically [|
    layout "notes" |> withRatio 1
    layout "todo" |> withRatio 1
    layout "timer" |> withRatio 1
  |]

let private bottomRowLayout =
  layout "bottom-row"
  |> splitVertically [| layout "info" |> withRatio 1; layout "avatar" |> withRatio 1 |]

let private sessionLayout =
  layout "session"
  |> splitHorizontally [| layout "top" |> withRatio 2; layout "bottom" |> withRatio 1 |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(ctx: RenderContext) =
        let slotPort = getPort ctx.Viewport sessionLayout
        let topPort = getPort ctx.Viewport topRowLayout
        let bottomPort = getPort ctx.Viewport bottomRowLayout

        ctx.Render(
          { new IWidget with
              member _.Render(ctx) =
                let notesFocusState =
                  match model.Focus, Notes.capturesInput model.Notes with
                  | 1, true -> Capturing
                  | 1, false -> Focused
                  | _ -> Unfocused

                let focusStateFor n =
                  match model.Focus = n with
                  | true -> Focused
                  | false -> Unfocused

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
                    (withPanelKeys (TodoList.widget model.TodoList) (TodoList.keyMap model.TodoList) (model.Focus = 2)),
                  topPort "todo"
                )

                ctx.Render(
                  focusableBox
                    "Timer"
                    3
                    (focusStateFor 3)
                    (withPanelKeys (Timer.widget model.Timer) (Timer.keyMap model.Timer) (model.Focus = 3)),
                  topPort "timer"
                )
          },
          slotPort "top"
        )

        ctx.Render(
          { new IWidget with
              member _.Render(ctx) =
                let focusStateFor n =
                  match model.Focus = n with
                  | true -> Focused
                  | false -> Unfocused

                ctx.Render(
                  focusableBox
                    "Session Info"
                    4
                    (focusStateFor 4)
                    (withPanelKeys
                      (SessionInfo.widget model.SessionInfo)
                      (SessionInfo.keyMap model.SessionInfo)
                      (model.Focus = 4)),
                  bottomPort "info"
                )

                ctx.Render(
                  focusableBox
                    "Users"
                    5
                    (focusStateFor 5)
                    (withPanelKeys (Avatar.widget model.Avatar) (Avatar.keyMap model.Avatar) (model.Focus = 5)),
                  bottomPort "avatar"
                )
          },
          slotPort "bottom"
        )
  }
