open Elmish
open System
open SpectreTuff.Widgets

type Msg =
  | InputMsg of Input.Msg
  | LogicMsg of Logic.Msg
  | ListMsg of ListWidget.Msg
  | Exit

type Model = {
  LogicModel: Logic.Model
  ListModel: ListWidget.Model
  ExitEvent: Threading.ManualResetEventSlim
  LogModel: Log.Model
}

let exitEvent = new Threading.ManualResetEventSlim false

let init () =
  {
    LogicModel = { Count = 0 }
    ListModel = {
      index = 0
      items = [
        ListItem "F# Elmish"
        ListItem "Spectre.Tui"
        ListItem "List Widget"
        ListItem "Interactive"
        ListItem "Sandbox"
      ]
    }
    ExitEvent = exitEvent
    LogModel = Log.init ()
  },
  []

let update msg model =
  match msg with
  | InputMsg inputMsg ->
    match inputMsg with
    | Input.KeyPressed key ->
      match key.Key with
      | ConsoleKey.D1 -> model, Cmd.ofMsg (LogicMsg(Logic.Increment 1))
      | ConsoleKey.D5 -> model, Cmd.ofMsg (LogicMsg(Logic.Increment 5))
      | ConsoleKey.D2 -> model, Cmd.ofMsg (LogicMsg(Logic.Increment 2))
      | ConsoleKey.UpArrow -> model, Cmd.ofMsg (ListMsg ListWidget.Up)
      | ConsoleKey.DownArrow -> model, Cmd.ofMsg (ListMsg ListWidget.Down)
      | ConsoleKey.Q -> model, Cmd.ofMsg Exit
      | _ -> model, Cmd.none
  | LogicMsg logicMsg ->
    let logicModel, command = Logic.update logicMsg model.LogicModel
    { model with LogicModel = logicModel }, command
  | Exit ->
    model.ExitEvent.Set()
    model, []
  | ListMsg listMsg ->
    let listModel, command = ListWidget.update listMsg model.ListModel
    { model with ListModel = listModel }, []

open Spectre.Tui
open SpectreTuff.View
open SpectreTuff.Widgets
open SpectreTuff.Layout
open System.IO

let mainLayout =
  layout "main"
  |> splitHorizontally [|
    layout "top"
    |> withRatio 3
    |> splitVertically [| layout "left"; layout "right" |]
    layout "log" |> withRatio 1
  |]

let view (renderer: Renderer) model dispatch =
  renderer.Draw(fun ctx elapsed ->
    let getPort = getPort ctx.Viewport mainLayout

    ListWidget.view model.ListModel ctx (getPort "left")
    Logic.view model.LogicModel ctx (getPort "right")
    Log.view model.LogModel ctx (getPort "log"))

let traceToLog msg (model: Model) _ =
  Log.append (sprintf "%A" msg) model.LogModel

Console.Clear()
let terminal = Terminal.Create()
let renderer = Renderer terminal
renderer.NoTargetFps()

Program.mkProgram init update (view renderer)
|> Input.withKeyListener InputMsg
|> Program.withTrace traceToLog
|> Program.run

exitEvent.Wait()
