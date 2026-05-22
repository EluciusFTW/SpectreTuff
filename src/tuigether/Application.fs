module Application

open System
open Elmish
open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Page =
  | SessionListPage
  | SessionViewPage of SessionView.Model

type Model = {
  Page: Page
  SessionList: SessionList.Model
  User: string
  AvatarName: string
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
  | SetActiveDriverCompleted of Result<unit, string>
  | LeaveCompleted of Result<unit, string>
  | CreateCompleted of Result<string, string>
  | DeleteCompleted of Result<unit, string>
  | ToggleLog
  | Exit

type Panel = {
  Number: int
  Title: string
  LayoutSlot: string
  Focused: bool
  Boxed: bool
  CapturesInput: bool
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
  | SessionListPage -> [
      {
        Number = 1
        Title = "Sessions"
        LayoutSlot = "content"
        Focused = model.Focus = 1
        Boxed = true
        CapturesInput = false
        Widget = SessionList.widget model.SessionList
        KeyMap = SessionList.keyMap model.SessionList
        HandleKey = fun key -> SessionList.handleKey key model.SessionList |> Option.map SessionListMsg
        Update = fun _ _ -> None
      }
    ]
  | SessionViewPage viewModel -> [
      {
        Number = 1
        Title = "Session"
        LayoutSlot = "content"
        Focused = model.Focus = 1
        Boxed = false
        CapturesInput = SessionView.capturesInput viewModel
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
  let listModel, listCmd = SessionList.init user ()

  let avatarName =
    let envName = System.Environment.GetEnvironmentVariable("TUIGETHER_AVATAR")

    if System.String.IsNullOrWhiteSpace(envName) then
      let idx = System.Random.Shared.Next(SpectreTuff.Widgets.Avatar.library.Length)
      SpectreTuff.Widgets.Avatar.library.[idx].Name
    else
      let found =
        SpectreTuff.Widgets.Avatar.library
        |> List.tryFind (fun c -> System.String.Equals(c.Name, envName, System.StringComparison.OrdinalIgnoreCase))

      match found with
      | Some c -> c.Name
      | None ->
        let idx = System.Random.Shared.Next(SpectreTuff.Widgets.Avatar.library.Length)
        SpectreTuff.Widgets.Avatar.library.[idx].Name

  {
    Page = SessionListPage
    SessionList = listModel
    User = user
    AvatarName = avatarName
    Focus = 1
    LogVisible = false
    LogModel = Log.init ()
  },
  Cmd.map SessionListMsg listCmd

let update (client: Firebase.Database.FirebaseClient) (user: string) msg model =
  match msg with
  | InputMsg(Input.KeyPressed key) ->
    let panels = buildPanels model
    let focusedPanel = panels |> List.tryFind (fun p -> p.Number = model.Focus)
    let capturing = focusedPanel |> Option.exists (_.CapturesInput)

    match capturing with
    | true ->
      focusedPanel
      |> Option.bind (fun p -> p.HandleKey key)
      |> Option.map (fun msg -> model, Cmd.ofMsg msg)
      |> Option.defaultValue (model, [])
    | false ->
      match key.Key with
      | ConsoleKey.Q -> model, Cmd.ofMsg Exit
      | ConsoleKey.L -> model, Cmd.ofMsg ToggleLog
      | _ ->
        focusedPanel
        |> Option.bind (fun p -> p.HandleKey key)
        |> Option.map (fun msg -> model, Cmd.ofMsg msg)
        |> Option.defaultValue (model, [])

  | FirebaseMsg(Firebase.SessionsLoaded sessions) ->
    model, Cmd.ofMsg (SessionListMsg(SessionList.SessionsLoaded sessions))
  | FirebaseMsg(Firebase.SessionChanged(id, data)) ->
    let sessionListCmd = Cmd.ofMsg (SessionListMsg(SessionList.SessionChanged(id, data)))

    let sessionViewCmd =
      match model.Page with
      | SessionViewPage vm when vm.SessionId = id -> Cmd.ofMsg (SessionViewMsg(SessionView.UpdateSession data))
      | _ -> Cmd.none

    model, Cmd.batch [ sessionListCmd; sessionViewCmd ]
  | FirebaseMsg(Firebase.SessionRemoved id) -> model, Cmd.ofMsg (SessionListMsg(SessionList.SessionRemoved id))
  | FirebaseMsg(Firebase.ConnectionError e) -> model, Cmd.ofMsg (SessionListMsg(SessionList.LoadError e))

  | SessionListMsg SessionList.CreateNew ->
    model, Cmd.OfAsync.perform (fun () -> Firebase.createSession client user) () CreateCompleted
  | SessionListMsg SessionList.DeleteSelected when not model.SessionList.Sessions.IsEmpty ->
    let sessionId, _ = model.SessionList.Sessions.[model.SessionList.SelectedIndex]
    model, Cmd.OfAsync.perform (fun () -> Firebase.deleteSession client sessionId) () DeleteCompleted
  | SessionListMsg SessionList.OpenSelected when not model.SessionList.Sessions.IsEmpty ->
    let sessionId, sessionData = model.SessionList.Sessions.[model.SessionList.SelectedIndex]
    let viewModel = SessionView.init user sessionId sessionData

    {
      model with
          Page = SessionViewPage viewModel
    },
    Cmd.OfAsync.perform (fun () -> Firebase.joinSession client sessionId user model.AvatarName) () JoinCompleted
  | SessionListMsg SessionList.OpenSelected -> model, []
  | SessionListMsg lMsg ->
    let listModel, listCmd = SessionList.update lMsg model.SessionList
    { model with SessionList = listModel }, Cmd.map SessionListMsg listCmd

  | SessionViewMsg SessionView.GoBack ->
    let leaveCmd =
      match model.Page with
      | SessionViewPage viewModel ->
        Cmd.OfAsync.perform (fun () -> Firebase.leaveSession client viewModel.SessionId user) () LeaveCompleted
      | _ -> []

    { model with Page = SessionListPage }, leaveCmd

  | SessionViewMsg(SessionView.SetActiveDriver user) ->
    match model.Page with
    | SessionViewPage vm ->
      let cmd =
        if System.String.IsNullOrEmpty(user) then
          Cmd.OfAsync.attempt (fun () -> Firebase.clearActiveDriver client vm.SessionId) () (fun _ ->
            SetActiveDriverCompleted(Ok()))
        else
          Cmd.OfAsync.attempt (fun () -> Firebase.setActiveDriver client vm.SessionId user) () (fun _ ->
            SetActiveDriverCompleted(Ok()))

      Some(model, cmd)
    | _ -> None
    |> Option.defaultValue (model, [])

  | SetActiveDriverCompleted _ -> model, []

  | JoinCompleted result ->
    match model.Page with
    | SessionViewPage _ -> model, Cmd.ofMsg (SessionViewMsg(SessionView.JoinCompleted result))
    | _ -> model, []
  | LeaveCompleted _ -> model, []
  | CreateCompleted _ -> model, []
  | DeleteCompleted _ -> model, []
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
          | true ->
            let focusState =
              match panel.CapturesInput, panel.Focused with
              | true, _ -> Capturing
              | _, true -> Focused
              | _ -> Unfocused

            focusableBox panel.Title panel.Number focusState composedWidget :> IWidget
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
