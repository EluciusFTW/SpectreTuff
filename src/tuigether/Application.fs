module Application

open System
open Elmish
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.View
open SpectreTuff.Widgets

type Page =
  | SessionListPage of SessionList.Model
  | SessionViewPage of SessionView.Model

type Model = {
  Page: Page
  User: string
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
  | Exit

let exitEvent = new Threading.ManualResetEventSlim false

let private mainLayout =
  layout "main"
  |> splitHorizontally [| layout "content" |> withRatio 3; layout "log" |> withRatio 1 |]

let init (user: string) () =
  let listModel, listCmd = SessionList.init ()

  {
    Page = SessionListPage listModel
    User = user
    LogModel = Log.init ()
  },
  Cmd.map SessionListMsg listCmd

let update (client: Firebase.Database.FirebaseClient) (user: string) msg model =
  match msg with
  | InputMsg(Input.KeyPressed key) ->
    match key.Key with
    | ConsoleKey.Q -> model, Cmd.ofMsg Exit
    | ConsoleKey.UpArrow ->
      match model.Page with
      | SessionListPage _ -> model, Cmd.ofMsg (SessionListMsg SessionList.Up)
      | SessionViewPage _ -> model, []
    | ConsoleKey.DownArrow ->
      match model.Page with
      | SessionListPage _ -> model, Cmd.ofMsg (SessionListMsg SessionList.Down)
      | SessionViewPage _ -> model, []
    | ConsoleKey.Enter ->
      match model.Page with
      | SessionListPage listModel when not listModel.Sessions.IsEmpty ->
        model, Cmd.ofMsg (SessionListMsg SessionList.OpenSelected)
      | _ -> model, []
    | ConsoleKey.N ->
      match model.Page with
      | SessionListPage _ -> model, Cmd.OfAsync.perform (fun () -> Firebase.createSession client) () CreateCompleted
      | _ -> model, []
    | ConsoleKey.Escape
    | ConsoleKey.Backspace ->
      match model.Page with
      | SessionViewPage viewModel ->
        let leaveCmd =
          Cmd.OfAsync.perform (fun () -> Firebase.leaveSession client viewModel.SessionId user) () LeaveCompleted

        model, Cmd.batch [ Cmd.ofMsg (SessionViewMsg SessionView.GoBack); leaveCmd ]
      | _ -> model, []
    | _ -> model, []

  | FirebaseMsg(Firebase.SessionsLoaded sessions) ->
    model, Cmd.ofMsg (SessionListMsg(SessionList.SessionsLoaded sessions))
  | FirebaseMsg(Firebase.SessionChanged(id, data)) ->
    model, Cmd.ofMsg (SessionListMsg(SessionList.SessionChanged(id, data)))
  | FirebaseMsg(Firebase.SessionRemoved id) -> model, Cmd.ofMsg (SessionListMsg(SessionList.SessionRemoved id))
  | FirebaseMsg(Firebase.ConnectionError e) -> model, Cmd.ofMsg (SessionListMsg(SessionList.LoadError e))

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
  | SessionListMsg msg ->
    match model.Page with
    | SessionListPage listModel ->
      let listModel', cmd = SessionList.update msg listModel

      {
        model with
            Page = SessionListPage listModel'
      },
      Cmd.map SessionListMsg cmd
    | _ -> model, []

  | SessionViewMsg SessionView.GoBack ->
    let listModel, listCmd = SessionList.init ()

    {
      model with
          Page = SessionListPage listModel
    },
    Cmd.map SessionListMsg listCmd
  | SessionViewMsg msg ->
    match model.Page with
    | SessionViewPage viewModel ->
      let viewModel', cmd = SessionView.update msg viewModel

      {
        model with
            Page = SessionViewPage viewModel'
      },
      Cmd.map SessionViewMsg cmd
    | _ -> model, []

  | JoinCompleted result ->
    match model.Page with
    | SessionViewPage _ -> model, Cmd.ofMsg (SessionViewMsg(SessionView.JoinCompleted result))
    | _ -> model, []
  | LeaveCompleted _ -> model, []
  | CreateCompleted _ -> model, []

  | Exit ->
    exitEvent.Set()
    model, []

type AppView(model: Model) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let getPort = getPort ctx.Viewport mainLayout

      match model.Page with
      | SessionListPage listModel -> SessionList.view listModel ctx (getPort "content")
      | SessionViewPage viewModel -> SessionView.view viewModel ctx (getPort "content")

      Log.view model.LogModel ctx (getPort "log")

let view (renderer: Renderer) (model: Model) _dispatch =
  renderer.Draw(fun ctx _ -> ctx.Render(AppView model))

let traceToLog msg (model: Model) _ =
  Log.append (sprintf "%A" msg) model.LogModel
