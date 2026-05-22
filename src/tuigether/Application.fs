module Application

open System
open Elmish
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.View
open SpectreTuff.Widgets

type Model = {
  Count: int
  Status: string
  PendingWrite: bool
  KeyPressCount: int
  LastKey: string
  ExitEvent: Threading.ManualResetEventSlim
  LogModel: Log.Model
}

type Msg =
  | InputMsg of Input.Msg
  | FirebaseMsg of Firebase.Msg
  | WriteCompleted of Result<unit, string>
  | Exit

let exitEvent = new Threading.ManualResetEventSlim false

let private mainLayout =
  layout "main"
  |> splitHorizontally [|
    layout "counter" |> withRatio 3
    layout "log" |> withRatio 1
  |]

let init () =
  {
    Count = 0
    Status = "connecting…"
    PendingWrite = false
    KeyPressCount = 0
    LastKey = "(none)"
    ExitEvent = exitEvent
    LogModel = Log.init ()
  }, []

let update (client: global.Firebase.Database.FirebaseClient) msg model =
  match msg with
  | InputMsg (Input.KeyPressed key) ->
    let keyLabel = sprintf "Key=%A Char='%c'(%d)" key.Key key.KeyChar (int key.KeyChar)
    let model = { model with KeyPressCount = model.KeyPressCount + 1; LastKey = keyLabel }
    match key.Key with
    | ConsoleKey.Q -> model, Cmd.ofMsg Exit
    | ConsoleKey.Spacebar when not model.PendingWrite ->
      { model with PendingWrite = true },
      Cmd.OfAsync.perform (fun () -> Firebase.increment client) () WriteCompleted
    | _ when key.KeyChar = ' ' && not model.PendingWrite ->
      { model with PendingWrite = true },
      Cmd.OfAsync.perform (fun () -> Firebase.increment client) () WriteCompleted
    | _ -> model, []
  | FirebaseMsg (Firebase.CountUpdated n) ->
    { model with Count = n; Status = "connected" }, []
  | FirebaseMsg (Firebase.ConnectionError e) ->
    { model with Status = sprintf "error: %s" e }, []
  | WriteCompleted (Ok ()) -> { model with PendingWrite = false }, []
  | WriteCompleted (Error e) ->
    { model with PendingWrite = false; Status = sprintf "write failed: %s" e }, []
  | Exit ->
    model.ExitEvent.Set()
    model, []

type AppView(model: Model) =
  interface IWidget with
    member _.Render(ctx: RenderContext) =
      let getPort = getPort ctx.Viewport mainLayout
      let pending = if model.PendingWrite then " (writing…)" else ""
      let content =
        $"""
  Status:      {model.Status}{pending}
  Shared count:{model.Count}

  Keys read:   {model.KeyPressCount}
  Last key:    {model.LastKey}

  [space] press   [q] quit
        """
        |> textBox
        |> withMode TextBoxMode.MultiLine
      ctx.Render(
        box (Look.fromColor Color.Green)
        |> withTitle "tuigether"
        |> withInnerWidget content,
        getPort "counter")
      Log.view model.LogModel ctx (getPort "log")

let view (renderer: Renderer) (model: Model) _dispatch =
  renderer.Draw(fun ctx _ -> ctx.Render(AppView model))

let traceToLog msg (model: Model) _ =
  Log.append (sprintf "%A" msg) model.LogModel
