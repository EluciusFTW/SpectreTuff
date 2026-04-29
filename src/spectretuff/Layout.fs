namespace SpectreTuff

open Spectre.Tui

module Layout =
  type Direction =
    | Horizontal
    | Vertical

  let layout name =
    new Layout(name)

  let split direction children (layout: Layout) =
    match direction with
    | Horizontal -> layout.SplitRows children
    | Vertical -> layout.SplitColumns children

  let splitHorizontally = split Horizontal
  let splitVertically = split Vertical

  let getPort (area: Rectangle) (layout: Layout) name =
    layout.GetArea(area, name)

  let getLayout name (layout: Layout) =
    layout.GetLayout name

  let setVisibility value (layout: Layout) =
    layout.IsVisible <- value
    layout

  let show = setVisibility true
  let hide = setVisibility false
