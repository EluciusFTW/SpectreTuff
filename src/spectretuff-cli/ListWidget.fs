module ListWidget

open Spectre.Console
open SpectreTuff.Widgets
open Spectre.Tui

type Model = { index: int; items: ListItem list }

type Msg =
  | Up
  | Down
  | Delete
  | Add

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

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  let listW =
    listWidget model.items
    |> selectedIndex model.index
    |> withHighlightSymbol (LineExtensions.FromString ("> ", Style Color.Blue))
    |> withWrapAround true

  let listArea = Rectangle (area.X, area.Y, area.Width, area.Height - 1)
  RenderContextExtensions.Render (ctx, listW, listArea)

  let info = $"Selected: {model.index + 1} of {model.items.Length}"
  RenderContextExtensions.Render (
    ctx,
    Text (LineExtensions.FromString (info, Style Color.Green)),
    Rectangle (area.X, area.Bottom - 1, area.Width, 1))
