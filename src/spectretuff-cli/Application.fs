module Application

open Elmish
open Spectre.Tui
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  LogicModel: Logic.Model
  ListModel: ListWidget.Model
  ExitEvent: System.Threading.ManualResetEventSlim
  LogModel: Log.Model
}

type Msg =
  | InputMsg of Input.Msg
  | LogicMsg of Logic.Msg
  | ListMsg of ListWidget.Msg
  | Exit

let exitEvent = new System.Threading.ManualResetEventSlim false

let private mainLayout =
  layout "main"
  |> splitHorizontally [|
    layout "top"
    |> withRatio 3
    |> splitVertically [| layout "left"; layout "right" |]
    layout "log" |> withRatio 1
  |]

type Application(model: Model) =
  interface IWidget with
    member _.Render(context: RenderContext) =
      let getPort = getPort context.Viewport mainLayout
      ListWidget.view model.ListModel context (getPort "left")
      Logic.view model.LogicModel context (getPort "right")
      Log.view model.LogModel context (getPort "log")

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

let update msg (model: Model) =
  match msg with
  | InputMsg inputMsg ->
    match inputMsg with
    | Input.KeyPressed key ->
      match key.Key with
      | System.ConsoleKey.D1 -> model, Cmd.ofMsg (LogicMsg(Logic.Increment 1))
      | System.ConsoleKey.D5 -> model, Cmd.ofMsg (LogicMsg(Logic.Increment 5))
      | System.ConsoleKey.D2 -> model, Cmd.ofMsg (LogicMsg(Logic.Increment 2))
      | System.ConsoleKey.UpArrow -> model, Cmd.ofMsg (ListMsg ListWidget.Up)
      | System.ConsoleKey.DownArrow -> model, Cmd.ofMsg (ListMsg ListWidget.Down)
      | System.ConsoleKey.Q -> model, Cmd.ofMsg Exit
      | _ -> model, Cmd.none
  | LogicMsg logicMsg ->
    let logicModel, command = Logic.update logicMsg model.LogicModel
    { model with LogicModel = logicModel }, command
  | Exit ->
    model.ExitEvent.Set()
    model, []
  | ListMsg listMsg ->
    let listModel, _ = ListWidget.update listMsg model.ListModel
    { model with ListModel = listModel }, []

let view (renderer: Renderer) (model: Model) dispatch =
  renderer.Draw(fun ctx _ -> ctx.Render(Application model))

let traceToLog msg (model: Model) _ =
  Log.append (sprintf "%A" msg) model.LogModel
