module Application

open System
open Elmish
open Firebase.Database
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
  Exiting: bool
  Notify: string -> unit
}

type Msg =
  | InputMsg of Input.Msg
  | SessionListMsg of SessionList.Msg
  | SessionViewMsg of SessionView.Msg
  | LeaveFinalized
  | ToggleLog
  | Tick
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
        CapturesInput = SessionList.capturesInput model.SessionList
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
        Update = fun _ _ -> None
      }
    ]

let init (client: FirebaseClient) (user: string) (notify: string -> unit) () =
  let listModel, listCmd = SessionList.init client user ()

  let avatarName = Journey.resolveName ()

  {
    Page = SessionListPage
    SessionList = listModel
    User = user
    AvatarName = avatarName
    Focus = 1
    LogVisible = false
    LogModel = Log.init ()
    Exiting = false
    Notify = notify
  },
  Cmd.map SessionListMsg listCmd

let private subMap (wrap: 'a -> 'b) (subs: (string list * (Dispatch<'a> -> IDisposable)) list) =
  subs
  |> List.map (fun (key, start) -> key, (fun (dispatch: Dispatch<'b>) -> start (wrap >> dispatch)))

let private handleSessionListOutMsg
  (client: FirebaseClient)
  (user: string)
  (avatarName: string)
  (model: Model)
  (out: SessionList.OutMsg option)
  : Model * Cmd<Msg> =
  match out with
  | Some(SessionList.OpenSession(sessionId, sessionData)) ->
    let viewModel, viewCmd = SessionView.init client user avatarName sessionId sessionData model.Notify

    {
      model with
          Page = SessionViewPage viewModel
    },
    Cmd.map SessionViewMsg viewCmd
  | None -> model, []

let private leaveFinalizeCmd (client: FirebaseClient) (sessionId: string) (user: string) (wasStarted: bool) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () ->
      async {
        let! result = Firebase.Users.leaveAndCheckLast client sessionId user

        match result, wasStarted with
        | Ok true, true -> do! Firebase.Sessions.setStatus client sessionId Session.Status.Finished
        | _ -> ()
      })
    ()
    (fun () -> LeaveFinalized)

let private handleSessionViewOutMsg
  (client: FirebaseClient)
  (model: Model)
  (out: SessionView.OutMsg option)
  : Model * Cmd<Msg> =
  match out with
  | Some(SessionView.LeaveSession(sessionId, user, wasStarted)) ->
    { model with Page = SessionListPage }, leaveFinalizeCmd client sessionId user wasStarted
  | None -> model, []

let update (client: FirebaseClient) (user: string) msg model =
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

  | SessionListMsg lMsg ->
    let listModel, listCmd, outMsg = SessionList.update lMsg model.SessionList

    let modelAfterList = { model with SessionList = listModel }
    let modelAfterOut, outCmd = handleSessionListOutMsg client user model.AvatarName modelAfterList outMsg
    modelAfterOut, Cmd.batch [ Cmd.map SessionListMsg listCmd; outCmd ]

  | SessionViewMsg vMsg ->
    match model.Page with
    | SessionViewPage viewModel ->
      let m, sessionCmd, outMsg = SessionView.update vMsg viewModel
      let modelAfterView = { model with Page = SessionViewPage m }
      let modelAfterOut, outCmd = handleSessionViewOutMsg client modelAfterView outMsg
      modelAfterOut, Cmd.batch [ Cmd.map SessionViewMsg sessionCmd; outCmd ]
    | _ -> model, []

  | LeaveFinalized ->
    match model.Exiting with
    | true ->
      exitEvent.Set()
      model, []
    | false -> model, []

  | ToggleLog ->
    {
      model with
          LogVisible = not model.LogVisible
    },
    []

  | Tick -> model, []

  | Exit ->
    match model.Page, model.Exiting with
    | SessionViewPage _, false -> { model with Exiting = true }, Cmd.ofMsg (SessionViewMsg SessionView.GoBack)
    | _ ->
      exitEvent.Set()
      model, []

let subscriptions (model: Model) =
  match model.Page with
  | SessionListPage -> SessionList.subscriptions model.SessionList |> subMap SessionListMsg
  | SessionViewPage vm -> SessionView.subscriptions vm |> subMap SessionViewMsg

type AppView(model: Model) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let panels = buildPanels model
      let slotPort = getPort ctx.Viewport (mainLayout model.LogVisible)

      for panel in panels do
        let composedWidget =
          { new IWidget with
              member _.Render(ctx) =
                match panel.Boxed with
                | true ->
                  let port = getPort ctx.Viewport panelInnerLayout
                  ctx.Render(panel.Widget, port "content")
                  ctx.Render(help [ panel.KeyMap ] |> leftAligned, port "keys")
                | false -> ctx.Render(panel.Widget, ctx.Viewport)
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

      let helpMaps =
        match model.Page with
        | SessionViewPage viewModel ->
          [ SessionView.keyMap viewModel ]
          @ SessionView.helpKeyMaps viewModel
          @ [ globalKeyMap ]
        | _ -> [ globalKeyMap ]

      ctx.Render(help helpMaps |> leftAligned, slotPort "help")

// Spectre.Tui's AnsiTerminal keeps mutable buffer/state shared across writes
// and is not thread-safe. Subscription callbacks (Firebase observables, async
// completions) can dispatch from thread-pool threads, so view may be invoked
// concurrently. Serialize draws here.
let private renderLock = obj ()

let view (renderer: Renderer) (model: Model) _dispatch =
  lock renderLock (fun () -> renderer.Draw(fun ctx _ -> ctx.Render(AppView model)))

let traceToLog msg (model: Model) _ =
  match msg with
  | Tick -> ()
  | _ -> Log.append (sprintf "%A" msg) model.LogModel
