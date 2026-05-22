module Application

open System
open Elmish
open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Page =
  | SessionListPage of SessionList.Model
  | SessionViewPage of SessionView.Model

type Model = {
  Page: Page
  User: string
  Focus: int
  LogVisible: bool
  LogModel: Log.Model
}

type Msg =
  | InputMsg of Input.Msg
  | FirebaseMsg of Firebase.Msg
  | SessionListMsg of SessionList.Msg
  | SessionViewMsg of SessionView.Msg
  | JoinCompleted of Result<unit, string>
  | LeaveCompleted of Result<unit, string>
  | CreateCompleted of Result<string, string>
  | ToggleLog
  | Exit

type Panel = {
  Number: int
  Title: string
  LayoutSlot: string
  Focused: bool
  Boxed: bool
  Widget: IWidget
  KeyMap: IKeyMap
  HandleKey: ConsoleKeyInfo -> Msg option
  Update: Msg -> Model -> (Model * Cmd<Msg>) option
}

let exitEvent = new Threading.ManualResetEventSlim false

let private panelInnerLayout =
  layout "panel-inner"
  |> splitHorizontally [| layout "content"; layout "keys" |> withFixedSize (Some 1) |]

let private mainLayout (logVisible: bool) =
  layout "main"
  |> splitHorizontally [|
    layout "top"
    |> splitVertically [|
      layout "content" |> withRatio 3
      layout "log"
      |> withRatio 1
      |> (match logVisible with
          | true -> show
          | false -> hide)
    |]
    layout "help" |> withFixedSize (Some 1)
  |]

let private globalKeyMap: IKeyMap =
  { new IKeyMap with
      member _.Help() =
        seq {
          KeyBinding(Keys = ResizeArray [ KeyPress.For 'q' ], Help = "quit")
          KeyBinding(Keys = ResizeArray [ KeyPress.For 'l' ], Help = "toggle log")
        }
  }

let private buildPanels (model: Model) : Panel list =
  match model.Page with
  | SessionListPage listModel -> [
      {
        Number = 1
        Title = "Sessions"
        LayoutSlot = "content"
        Focused = model.Focus = 1
        Boxed = true
        Widget = SessionList.widget listModel
        KeyMap = SessionList.keyMap listModel
        HandleKey = fun key -> SessionList.handleKey key listModel |> Option.map SessionListMsg
        Update =
          fun msg model ->
            match msg with
            | SessionListMsg lMsg ->
              match model.Page with
              | SessionListPage listModel ->
                let m, cmd = SessionList.update lMsg listModel
                Some({ model with Page = SessionListPage m }, Cmd.map SessionListMsg cmd)
              | _ -> None
            | _ -> None
      }
    ]
  | SessionViewPage viewModel -> [
      {
        Number = 1
        Title = "Session"
        LayoutSlot = "content"
        Focused = model.Focus = 1
        Boxed = false
        Widget = SessionView.widget viewModel
        KeyMap = SessionView.keyMap viewModel
        HandleKey = fun key -> SessionView.handleKey key viewModel |> Option.map SessionViewMsg
        Update =
          fun msg model ->
            match msg with
            | SessionViewMsg vMsg ->
              match model.Page with
              | SessionViewPage viewModel ->
                let m, cmd = SessionView.update vMsg viewModel
                Some({ model with Page = SessionViewPage m }, Cmd.map SessionViewMsg cmd)
              | _ -> None
            | _ -> None
      }
    ]

let init (user: string) () =
  let listModel, listCmd = SessionList.init ()

  {
    Page = SessionListPage listModel
    User = user
    Focus = 1
    LogVisible = true
    LogModel = Log.init ()
  },
  Cmd.map SessionListMsg listCmd

let update (client: Firebase.Database.FirebaseClient) (user: string) msg model =
  match msg with
  | InputMsg(Input.KeyPressed key) ->
    match key.Key with
    | ConsoleKey.Q -> model, Cmd.ofMsg Exit
    | ConsoleKey.L -> model, Cmd.ofMsg ToggleLog
    | _ ->
      buildPanels model
      |> List.tryFind (fun p -> p.Number = model.Focus)
      |> Option.bind (fun p -> p.HandleKey key)
      |> Option.map (fun msg -> model, Cmd.ofMsg msg)
      |> Option.defaultValue (model, [])

  | FirebaseMsg(Firebase.SessionsLoaded sessions) ->
    model, Cmd.ofMsg (SessionListMsg(SessionList.SessionsLoaded sessions))
  | FirebaseMsg(Firebase.SessionChanged(id, data)) ->
    model, Cmd.ofMsg (SessionListMsg(SessionList.SessionChanged(id, data)))
  | FirebaseMsg(Firebase.SessionRemoved id) -> model, Cmd.ofMsg (SessionListMsg(SessionList.SessionRemoved id))
  | FirebaseMsg(Firebase.ConnectionError e) -> model, Cmd.ofMsg (SessionListMsg(SessionList.LoadError e))

  | SessionListMsg SessionList.CreateNew ->
    model, Cmd.OfAsync.perform (fun () -> Firebase.createSession client) () CreateCompleted
  | SessionListMsg SessionList.OpenSelected ->
    match model.Page with
    | SessionListPage listModel when not listModel.Sessions.IsEmpty ->
      let sessionId, sessionData = listModel.Sessions.[listModel.SelectedIndex]
      let viewModel = SessionView.init user sessionId sessionData

      {
        model with
            Page = SessionViewPage viewModel
      },
      Cmd.OfAsync.perform (fun () -> Firebase.joinSession client sessionId user) () JoinCompleted
    | _ -> model, []

  | SessionViewMsg SessionView.GoBack ->
    let listModel, listCmd = SessionList.init ()

    let leaveCmd =
      match model.Page with
      | SessionViewPage viewModel ->
        Cmd.OfAsync.perform (fun () -> Firebase.leaveSession client viewModel.SessionId user) () LeaveCompleted
      | _ -> []

    {
      model with
          Page = SessionListPage listModel
    },
    Cmd.batch [ Cmd.map SessionListMsg listCmd; leaveCmd ]

  | JoinCompleted result ->
    match model.Page with
    | SessionViewPage _ -> model, Cmd.ofMsg (SessionViewMsg(SessionView.JoinCompleted result))
    | _ -> model, []
  | LeaveCompleted _ -> model, []
  | CreateCompleted _ -> model, []
  | ToggleLog ->
    {
      model with
          LogVisible = not model.LogVisible
    },
    []

  | Exit ->
    exitEvent.Set()
    model, []

  | _ ->
    buildPanels model
    |> List.tryPick (fun p -> p.Update msg model)
    |> Option.defaultValue (model, [])

type AppView(model: Model) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let panels = buildPanels model
      let slotPort = getPort ctx.Viewport (mainLayout model.LogVisible)

      for panel in panels do
        let composedWidget =
          { new IWidget with
              member _.Render(ctx) =
                let port = getPort ctx.Viewport panelInnerLayout
                ctx.Render(panel.Widget, port "content")
                ctx.Render(help [ panel.KeyMap ] |> leftAligned, port "keys")
          }

        let renderedPanel: IWidget =
          match panel.Boxed with
          | true -> focusableBox panel.Title panel.Number panel.Focused composedWidget :> IWidget
          | false -> composedWidget

        ctx.Render(renderedPanel, slotPort panel.LayoutSlot)

      match model.LogVisible with
      | true -> Log.view model.LogModel ctx (slotPort "log")
      | false -> ()

      ctx.Render(help [ globalKeyMap ] |> leftAligned, slotPort "help")

let view (renderer: Renderer) (model: Model) _dispatch =
  renderer.Draw(fun ctx _ -> ctx.Render(AppView model))

let traceToLog msg (model: Model) _ =
  Log.append (sprintf "%A" msg) model.LogModel
