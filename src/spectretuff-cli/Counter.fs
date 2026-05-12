module Counter

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

let private counterInfo model =
  $""" 
  Increase the count by pressing 1,2,5. 

  Current Count: {model.Count}
  """
  |> textBox
  |> withMode TextBoxMode.MultiLine

let private widget model =
  box Spectre.Console.Color.Red
  |> withTitle "Outer Box"
  |> withInnerWidget (
    box Spectre.Console.Color.Purple
    |> withTitle "Inner Box"
    |> withInnerWidget (counterInfo model)
  )

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  ctx.Render(widget model, area)
