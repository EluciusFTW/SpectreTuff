module ListWidget

open System
open Spectre.Console
open Spectre.Tui
open SpectreTuff.Widgets

type Model = { index: int; items: ListItem list }

type Msg =
  | Up
  | Down
  | Delete
  | Add

let handleKey (key: ConsoleKeyInfo) : Msg option =
  match key.Key with
  | ConsoleKey.UpArrow -> Some Up
  | ConsoleKey.DownArrow -> Some Down
  | _ -> None

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

let widget (model: Model) =
  listWidget model.items
  |> selectedIndex model.index
  |> withHighlightSymbol (LineExtensions.FromString ("> ", Style Color.Blue))
  |> wrapAround
