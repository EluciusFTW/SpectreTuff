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
  |> splitVertically [|
    layout "left"
    layout "right"
    |> splitHorizontally [| layout "one"; layout "two" |> hide; layout "three" |]
  |]

let widgets = [
  "green", box Spectre.Console.Color.Green |> withTitle "Green Box"
  "red", box Spectre.Console.Color.Red |> withTitle "Other Box"
]

let view (renderer: Renderer) model dispatch =

  renderer.Draw(fun ctx elapsed ->
    let count = model.LogicModel.Count
    let getPort = getPort ctx.Viewport mainLayout

    // render w1
    match count % 4 with
    | 0 -> ctx.Render(widgets.Head |> snd, getPort "left")
    | 1 -> ctx.Render(widgets.Head |> snd, getPort "one")
    | 2 -> ctx.Render(widgets.Head |> snd, getPort "three")
    | 3 -> ()

    // render w2
    match count % 2 with
    | 0 ->
      let port = getPort "one"
      ctx.Render(widgets.Tail.Head |> snd, port)
      ctx.Render(Text(LineExtensions.FromString $"Current Count: {model.LogicModel.Count}"), getInner port)
    | 1 -> ctx.Render(widgets.Tail.Head |> snd, getPort "three")

    if count % 4 = 3 then
      ctx.Render(box Spectre.Console.Color.Pink1, shrink ctx.Viewport 3 3)
      ctx.Render(new ClearWidget ' ', shrink ctx.Viewport 4 4))

  ListWidget.view renderer model.ListModel dispatch

let logTrace msg model subs =
  eprintfn "Msg: %A" msg
  eprintfn "Model: %A" model
  eprintfn "Subs: %A" subs

let logFile (path: string) msg model subs =
  let lines = [
    sprintf "Msg: %A" msg
    sprintf "Model: %A" model
    sprintf "Subs: %A" subs
    "---"
  ]

  File.AppendAllLines(path, lines)

let logToDefaultFile = logFile "./trace.log"

let noLog _ __ ___ =
  ()

Console.Clear()
let terminal = Terminal.Create()
let renderer = Renderer terminal
renderer.SetTargetFps 144

Program.mkProgram init update (view renderer)
|> Input.withKeyListener InputMsg
|> Program.withTrace logToDefaultFile
|> Program.run

exitEvent.Wait()
