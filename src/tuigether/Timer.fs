module Timer

open System
open Elmish
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

let private defaultRemaining = TimeSpan.FromMinutes 10.0

type State =
  | Running
  | Stopped

type Model = { Remaining: TimeSpan; State: State }

type Msg =
  | Start
  | Stop
  | Tick
  | Reset

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (CharKey 's') (fun model ->
    match model.State with
    | Running -> {
        Description = "stop"
        Message = Some Stop
      }
    | Stopped -> {
        Description = "start"
        Message = Some Start
      })
  KeyBinding.dynamic (CharKey 'r') (fun model -> {
    Description = "reset"
    Message =
      match model.State with
      | Stopped -> Some Reset
      | Running -> None
  })
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let private tickCmd: Cmd<Msg> = Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 1000 }) () (fun () -> Tick)

let init () = {
  Remaining = defaultRemaining
  State = Stopped
}

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
        {
          model with
              Remaining = TimeSpan.Zero
              State = Stopped
        },
        []
      else
        { model with Remaining = next }, tickCmd
  | Reset ->
    {
      model with
          Remaining = defaultRemaining
    },
    []

let private formatTime (t: TimeSpan) =
  sprintf "%02d:%02d" (int t.TotalMinutes) t.Seconds

let private timerInfo model =
  let stateLabel =
    match model.State with
    | Running -> "running"
    | Stopped -> "stopped"

  $"""
  {formatTime model.Remaining}

  {stateLabel}
  """
  |> textBox
  |> withMode TextBoxMode.MultiLine

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) =
  timerInfo model :> IWidget
