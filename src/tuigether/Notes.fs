module Notes

open System
open Spectre.Tui
open Keymap
open SpectreTuff.Widgets

type Model = unit

type Msg = | NoOp

let init () : Model =
  ()

let update _msg model =
  model, []

let private bindings: KeyBinding<Model, Msg> list = []

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (_model: Model) : IWidget =
  ofString "(notes — coming soon)" :> IWidget
