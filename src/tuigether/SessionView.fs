module SessionView

open System
open Elmish
open Spectre.Tui
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
  if key.KeyChar >= '1' && key.KeyChar <= '9' then
    let n = int key.KeyChar - int '0'
    if n <= panelCount then Some(FocusPanel n) else None
  else
    None

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
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
  let focusedKeyMap =
    match model.Focus with
    | 1 -> Notes.keyMap model.Notes
    | 2 -> TodoList.keyMap model.TodoList
    | 3 -> Timer.keyMap model.Timer
    | 4 -> SessionInfo.keyMap model.SessionInfo
    | 5 -> Avatar.keyMap model.Avatar
    | _ -> KeyBinding.toKeyMap [] model

  let outerKeyMap = KeyBinding.toKeyMap outerBindings model

  { new Spectre.Tui.App.IKeyMap with
      member _.Help() =
        Seq.append (outerKeyMap.Help()) (focusedKeyMap.Help())
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
                ctx.Render(focusableBox "Notes" 1 (model.Focus = 1) (Notes.widget model.Notes), topPort "notes")
                ctx.Render(focusableBox "Todo" 2 (model.Focus = 2) (TodoList.widget model.TodoList), topPort "todo")
                ctx.Render(focusableBox "Timer" 3 (model.Focus = 3) (Timer.widget model.Timer), topPort "timer")
          },
          slotPort "top"
        )

        ctx.Render(
          { new IWidget with
              member _.Render(ctx) =
                ctx.Render(
                  focusableBox "Session Info" 4 (model.Focus = 4) (SessionInfo.widget model.SessionInfo),
                  bottomPort "info"
                )

                ctx.Render(focusableBox "Users" 5 (model.Focus = 5) (Avatar.widget model.Avatar), bottomPort "avatar")
          },
          slotPort "bottom"
        )
  }
