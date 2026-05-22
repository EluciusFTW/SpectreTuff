module Application

open System
open Elmish
open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  CounterModel: Counter.Model
  ListModel: ListWidget.Model
  TimerModel: Timer.Model
  AvatarModel: AvatarDemo.Model
  ExitEvent: Threading.ManualResetEventSlim
  LogModel: Log.Model
  Focus: int
  LogVisible: bool
}

type Msg =
  | InputMsg of Input.Msg
  | CounterMsg of Counter.Msg
  | ListMsg of ListWidget.Msg
  | TimerMsg of Timer.Msg
  | AvatarMsg of AvatarDemo.Msg
  | ToggleLog
  | Exit

type Panel = {
  Number: int
  Title: string
  LayoutSlot: string
  Focused: bool
  Widget: IWidget
  KeyMap: IKeyMap
  HandleKey: ConsoleKeyInfo -> Msg option
  Update: Msg -> Model -> (Model * Cmd<Msg>) option
}

let private panelInnerLayout =
  layout "panel-inner"
  |> splitHorizontally [| layout "content"; layout "keys" |> withFixedSize (Some 1) |]

let exitEvent = new System.Threading.ManualResetEventSlim false

let private mainLayout (model: Model) =
  layout "main"
  |> splitHorizontally [|
    layout "top"
    |> withRatio 3
    |> splitVertically [| layout "left"; layout "center"; layout "right"; layout "avatar" |]
    layout "log" |> withRatio 1 |> (if model.LogVisible then show else hide)
    layout "help" |> withFixedSize (Some 1)
  |]

let private globalKeyMap: IKeyMap =
  { new IKeyMap with
      member _.Help() =
        seq {
          yield KeyBinding(Keys = ResizeArray [ KeyPress.For 'q' ], Help = "quit")
          yield KeyBinding(Keys = ResizeArray [ KeyPress.For 'l' ], Help = "toggle log")
        }
  }

let private buildPanels (model: Model) =
  let focused n =
    model.Focus = n

  [
    {
      Number = 1
      Title = "List"
      LayoutSlot = "left"
      Focused = focused 1
      Widget = ListWidget.widget model.ListModel
      KeyMap = ListWidget.keyMap model.ListModel
      HandleKey = fun key -> ListWidget.handleKey key model.ListModel |> Option.map ListMsg
      Update =
        fun msg model ->
          match msg with
          | ListMsg lMsg ->
            let m, cmd = ListWidget.update lMsg model.ListModel
            Some({ model with ListModel = m }, cmd)
          | _ -> None
    }
    {
      Number = 2
      Title = "Counter"
      LayoutSlot = "right"
      Focused = focused 2
      Widget = Counter.widget model.CounterModel
      KeyMap = Counter.keyMap model.CounterModel
      HandleKey = fun key -> Counter.handleKey key model.CounterModel |> Option.map CounterMsg
      Update =
        fun msg model ->
          match msg with
          | CounterMsg cMsg ->
            let m, cmd = Counter.update cMsg model.CounterModel
            Some({ model with CounterModel = m }, cmd)
          | _ -> None
    }
    {
      Number = 3
      Title = "Timer"
      LayoutSlot = "center"
      Focused = focused 3
      Widget = Timer.widget model.TimerModel
      KeyMap = Timer.keyMap model.TimerModel
      HandleKey = fun key -> Timer.handleKey key model.TimerModel |> Option.map TimerMsg
      Update =
        fun msg model ->
          match msg with
          | TimerMsg tMsg ->
            let m, cmd = Timer.update tMsg model.TimerModel
            Some({ model with TimerModel = m }, Cmd.map TimerMsg cmd)
          | _ -> None
    }
    {
      Number = 4
      Title = "Avatar"
      LayoutSlot = "avatar"
      Focused = focused 4
      Widget = AvatarDemo.widget model.AvatarModel
      KeyMap = AvatarDemo.keyMap model.AvatarModel
      HandleKey = fun key -> AvatarDemo.handleKey key model.AvatarModel |> Option.map AvatarMsg
      Update =
        fun msg model ->
          match msg with
          | AvatarMsg aMsg ->
            let m, cmd = AvatarDemo.update aMsg model.AvatarModel
            Some({ model with AvatarModel = m }, cmd)
          | _ -> None
    }
  ]

type Application(model: Model) =
  let panels = buildPanels model

  interface IWidget with
    member _.Render(context: RenderContext) =
      let lyt = mainLayout model
      let slotPort = getPort context.Viewport lyt

      for panel in panels do
        let composedWidget =
          { new IWidget with
              member _.Render(ctx) =
                let port = getPort ctx.Viewport panelInnerLayout
                ctx.Render(panel.Widget, port "content")

                if panel.Focused then
                  ctx.Render(help [ panel.KeyMap ] |> leftAligned, port "keys")
          }

        context.Render(focusableBox panel.Title panel.Number panel.Focused composedWidget, slotPort panel.LayoutSlot)

      if model.LogVisible then
        Log.view model.LogModel context (slotPort "log")

      context.Render(help [ globalKeyMap ] |> leftAligned, slotPort "help")

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
    AvatarModel = AvatarDemo.init ()
    ExitEvent = exitEvent
    LogModel = Log.init ()
    Focus = 1
    LogVisible = true
  },
  []

let private tryFocusNumber (key: ConsoleKeyInfo) =
  if key.KeyChar >= '1' && key.KeyChar <= '9' then
    Some(int key.KeyChar - int '0')
  else
    None

let update msg (model: Model) =
  let panels = buildPanels model

  match msg with
  | InputMsg inputMsg ->
    match inputMsg with
    | Input.KeyPressed key ->
      match key.Key with
      | ConsoleKey.Q -> model, Cmd.ofMsg Exit
      | ConsoleKey.L -> model, Cmd.ofMsg ToggleLog
      | _ ->
        match tryFocusNumber key with
        | Some number when panels |> List.exists (fun p -> p.Number = number) -> { model with Focus = number }, []
        | _ ->
          panels
          |> List.tryFind (fun p -> p.Number = model.Focus)
          |> Option.bind (fun p -> p.HandleKey key)
          |> Option.map (fun msg -> model, Cmd.ofMsg msg)
          |> Option.defaultValue (model, Cmd.none)
  | ToggleLog ->
    {
      model with
          LogVisible = not model.LogVisible
    },
    []
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
