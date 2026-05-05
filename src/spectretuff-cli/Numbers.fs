module Logic

open Spectre.Tui
open SpectreTuff.View
open SpectreTuff.Widgets

type Model = { Count: int }

type Msg =
  | Increment of int
  | Decrement

let update msg model =
  match msg with
  | Increment n -> { model with Count = model.Count + n }, []
  | Decrement -> { model with Count = model.Count - 1 }, []

let private widget =
  box Spectre.Console.Color.Red
  |> withTitle "Outer Box"
  |> withInnerWidget (
    box Spectre.Console.Color.Purple
    |> withTitle "Inner Box"
    |> withInnerWidget (ofString "one")
  )

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  ctx.Render (widget, area)
  ctx.Render (Text (LineExtensions.FromString $"Current Count: {model.Count}"), getInner area)
