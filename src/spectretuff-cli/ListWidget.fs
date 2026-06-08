module ListWidget

open System
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = { index: int; items: ListItem list }

type Msg =
  | Up
  | Down
  | Delete
  | Add

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" Down
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let update msg model =
  let itemCount = model.items.Length

  if itemCount = 0 then
    model, []
  else
    match msg with
    | Up ->
      let nextIndex = (model.index - 1 + itemCount) % itemCount
      { model with index = nextIndex }, []
    | Down ->
      let nextIndex = (model.index + 1) % itemCount
      { model with index = nextIndex }, []
    | Delete ->
      {
        model with
            items = model.items |> List.removeAt model.index
            index = (model.index + 1) % model.items.Length - 1
      },
      []
    | Add ->
      {
        model with
            items = model.items |> List.insertAt model.index (ListItem "Added Item")
            index = (model.index + 1) % model.items.Length - 1
      },
      []

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) =
  list model.items
  |> selectedIndex model.index
  |> withHighlightSymbol (LineExtensions.FromString("> ", Style Color.Blue))
  |> wrapAround
  :> IWidget
