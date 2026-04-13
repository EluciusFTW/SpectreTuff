open Elmish
open System

type Msg =
    | InputMsg of Input.Msg
    | LogicMsg of Logic.Msg
    | Exit

type Model =
    { LogicModel: Logic.Model
      ExitEvent: Threading.ManualResetEventSlim }

let exitEvent = new Threading.ManualResetEventSlim false

let init () =
    { LogicModel = { Count = 0 }
      ExitEvent = exitEvent },
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

open Spectre.Tui
open SpectreTuff.View
open SpectreTuff.Widgets
open SpectreTuff.Layout

let mainLayout =
    layout "main"
    |> splitVertically [|
        layout "left";
        layout "right"
        |> splitHorizontally [|
            layout "one";
            layout "two" |> hide;
            layout "three"
        |]
    |]

let widgets = [
    "green", box Spectre.Console.Color.Green
    "red", box Spectre.Console.Color.Red
]

let view (renderer: Renderer) model dispatch =
    renderer.Draw(fun ctx elapsed ->
        let count = model.LogicModel.Count

        // render w1
        match count % 4 with
        | 0 -> ctx.Render(widgets.Head |> snd, getPortFor ctx "left" mainLayout)
        | 1 -> ctx.Render(widgets.Head |> snd, getPortFor ctx "one" mainLayout)
        | 2 -> ctx.Render(widgets.Head |> snd, getPortFor ctx "three" mainLayout)
        | 3 -> ()

        // render w2
        match count % 2 with
        | 0 ->
            let port = getPortFor ctx "one" mainLayout
            ctx.Render(widgets.Tail.Head |> snd, port)
            ctx.Render(
                Text(LineExtensions.FromString $"Current Count: {model.LogicModel.Count}"),
                getInner port)
        | 1 -> ctx.Render(widgets.Tail.Head |> snd, getPortFor ctx "three" mainLayout)
    );

let logTrace msg model subs =
    eprintfn "Msg: %A" msg
    eprintfn "Model: %A" model
    eprintfn "Subs: %A" subs

let noLog _ __ ___ = ()

Console.Clear()
let terminal = Terminal.Create()
let renderer = Renderer terminal
renderer.SetTargetFps 144

Program.mkProgram init update (view renderer)
|> Input.withKeyListener InputMsg
|> Program.withTrace noLog
|> Program.run

exitEvent.Wait()
