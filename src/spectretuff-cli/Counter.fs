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

let private bindings: KeyBinding<Model, Msg> list = [
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

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) =
  box (Look.fromColor Color.Purple)
  |> withTitle "Counter"
  |> withInnerWidget (counterInfo model)
