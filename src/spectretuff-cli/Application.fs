module Application

open System
open Elmish
open Spectre.Tui
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  CounterModel: Counter.Model
  ListModel: ListWidget.Model
  TimerModel: Timer.Model
  ExitEvent: Threading.ManualResetEventSlim
  LogModel: Log.Model
  Focus: int
}

type Msg =
  | InputMsg of Input.Msg
  | CounterMsg of Counter.Msg
  | ListMsg of ListWidget.Msg
  | TimerMsg of Timer.Msg
  | Exit

type Panel = {
  Number: int
  Title: string
  LayoutSlot: string
  Focused: bool
  Widget: IWidget
  HandleKey: ConsoleKeyInfo -> Msg option
  Update: Msg -> Model -> (Model * Cmd<Msg>) option
}

let exitEvent = new System.Threading.ManualResetEventSlim false

let private mainLayout =
  layout "main"
  |> splitHorizontally [|
    layout "top"
    |> withRatio 3
    |> splitVertically [| layout "left"; layout "center"; layout "right" |]
    layout "log" |> withRatio 1
  |]

let private buildPanels (model: Model) = [
  {
    Number = 1
    Title = "List"
    LayoutSlot = "left"
    Focused = model.Focus = 1
    Widget = ListWidget.widget model.ListModel
    HandleKey = fun key -> ListWidget.handleKey key model.ListModel |> Option.map ListMsg
    Update = fun msg model ->
      match msg with
      | ListMsg lMsg ->
        let m, cmd = ListWidget.update lMsg model.ListModel
        Some ({ model with ListModel = m }, cmd)
      | _ -> None
  }
  {
    Number = 2
    Title = "Counter"
    LayoutSlot = "right"
    Focused = model.Focus = 2
    Widget = Counter.widget model.CounterModel
    HandleKey = fun key -> Counter.handleKey key model.CounterModel |> Option.map CounterMsg
    Update = fun msg model ->
      match msg with
      | CounterMsg cMsg ->
        let m, cmd = Counter.update cMsg model.CounterModel
        Some ({ model with CounterModel = m }, cmd)
      | _ -> None
  }
  {
    Number = 3
    Title = "Timer"
    LayoutSlot = "center"
    Focused = model.Focus = 3
    Widget = Timer.widget model.TimerModel
    HandleKey = fun key -> Timer.handleKey key model.TimerModel |> Option.map TimerMsg
    Update = fun msg model ->
      match msg with
      | TimerMsg tMsg ->
        let m, cmd = Timer.update tMsg model.TimerModel
        Some ({ model with TimerModel = m }, Cmd.map TimerMsg cmd)
      | _ -> None
  }
]

type Application(model: Model) =
  let panels = buildPanels model
  interface IWidget with
    member _.Render(context: RenderContext) =
      let getPort = getPort context.Viewport mainLayout
      for panel in panels do
        context.Render(focusableBox panel.Title panel.Number panel.Focused panel.Widget, getPort panel.LayoutSlot)
      Log.view model.LogModel context (getPort "log")

let init () =
  {
    CounterModel = { Count = 0 }
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
    TimerModel = Timer.init ()
    ExitEvent = exitEvent
    LogModel = Log.init ()
    Focus = 1
  },
  []

let private tryFocusNumber (key: ConsoleKeyInfo) =
  if key.KeyChar >= '1' && key.KeyChar <= '9' then Some(int key.KeyChar - int '0')
  else None

let update msg (model: Model) =
  let panels = buildPanels model
  match msg with
  | InputMsg inputMsg ->
    match inputMsg with
    | Input.KeyPressed key ->
      match key.Key with
      | ConsoleKey.Q -> model, Cmd.ofMsg Exit
      | _ ->
        match tryFocusNumber key with
        | Some number when panels |> List.exists (fun p -> p.Number = number) ->
          { model with Focus = number }, []
        | _ ->
          panels
          |> List.tryFind (fun p -> p.Number = model.Focus)
          |> Option.bind (fun p -> p.HandleKey key)
          |> Option.map (fun msg -> model, Cmd.ofMsg msg)
          |> Option.defaultValue (model, Cmd.none)
  | Exit ->
    model.ExitEvent.Set()
    model, []
  | _ ->
    panels
    |> List.tryPick (fun p -> p.Update msg model)
    |> Option.defaultValue (model, Cmd.none)

let view (renderer: Renderer) (model: Model) dispatch =
  renderer.Draw(fun ctx _ -> ctx.Render(Application model))

let traceToLog msg (model: Model) _ =
  Log.append (sprintf "%A" msg) model.LogModel
