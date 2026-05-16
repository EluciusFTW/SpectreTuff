module Timer

open System
open Elmish
open Spectre.Tui
open SpectreTuff.View
open SpectreTuff.Widgets

let private defaultRemaining = TimeSpan.FromMinutes 10.0

type State =
  | Running
  | Stopped

type Model = {
  Remaining: TimeSpan
  State: State
}

type Msg =
  | Start
  | Stop
  | Tick
  | Reset

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match key.KeyChar with
  | 's' ->
    match model.State with
    | Running -> Some Stop
    | Stopped -> Some Start
  | 'r' when model.State = Stopped -> Some Reset
  | _ -> None

let private tickCmd : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 1000 }) () (fun () -> Tick)

let init () = { Remaining = defaultRemaining; State = Stopped }

let update msg model =
  match msg with
  | Start -> { model with State = Running }, tickCmd
  | Stop -> { model with State = Stopped }, []
  | Tick ->
    match model.State with
    | Stopped -> model, []
    | Running ->
      let next = model.Remaining - TimeSpan.FromSeconds 1.0
      if next <= TimeSpan.Zero then
        { model with Remaining = TimeSpan.Zero; State = Stopped }, []
      else
        { model with Remaining = next }, tickCmd
  | Reset -> { model with Remaining = defaultRemaining }, []

let private formatTime (t: TimeSpan) =
  sprintf "%02d:%02d" (int t.TotalMinutes) t.Seconds

let widget (model: Model) =
  let stateLabel = match model.State with Running -> "running" | Stopped -> "stopped"
  let startStop = match model.State with Running -> "[s] stop" | Stopped -> "[s] start"
  let reset = if model.State = Stopped then "\n  [r] reset" else ""
  $"""
  {formatTime model.Remaining}

  {stateLabel}
  {startStop}{reset}
  """
  |> textBox
  |> withMode TextBoxMode.MultiLine
