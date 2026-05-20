module Counter

open System
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.View
open SpectreTuff.Widgets

type Model = { Count: int }

type Msg =
  | Increment of int
  | Decrement

let private bindings : KeyBinding<Model, Msg> list = [
  KeyBinding.create '+' "increment" (Increment 1)
  KeyBinding.create '-' "decrement" Decrement
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let update msg model =
  match msg with
  | Increment n -> { model with Count = model.Count + n }, []
  | Decrement -> { model with Count = model.Count - 1 }, []

let private counterInfo model =
  $"""
  Current Count: {model.Count}
  """
  |> textBox
  |> withMode TextBoxMode.MultiLine

let private innerLayout =
  layout "counter-inner"
  |> splitHorizontally [| layout "info" |> withRatio 3; layout "keys" |> withRatio 1 |]

let widget (model: Model) =
  let inner =
    { new IWidget with
        member _.Render(ctx) =
          let port = getPort ctx.Viewport innerLayout
          ctx.Render(counterInfo model, port "info")
          ctx.Render(KeyBinding.keys bindings model |> showKeys, port "keys") }
  box (Look.fromColor Color.Purple)
  |> withTitle "Counter"
  |> withInnerWidget inner
