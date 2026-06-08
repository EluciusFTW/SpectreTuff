namespace SpectreTuff.Widgets

open Spectre.Console
open Spectre.Tui
open SpectreTuff

[<AutoOpen>]
module Box =

  let box (look: Look) =
    BoxWidget(Look.toStyle look)

  let withBoxTitle (title: BoxTitle) (box: BoxWidget) =
    box.Titles.Add title
    box

  let withTitle title =
    withBoxTitle (BoxTitle(TextLine(TextSpan title), TitlePosition.Top, Justify.Left))

  let withBorder border (box: BoxWidget) =
    box.Border <- border
    box

  let withInnerWidget widget (box: BoxWidget) =
    box.Inner <- widget
    box

  type FocusState =
    | Unfocused
    | Focused
    | Capturing

  let focusableBox (title: string) (focusNumber: int) (focusState: FocusState) (innerWidget: IWidget) =
    let look =
      match focusState with
      | Unfocused -> Look.fromColor Color.Grey
      | Focused -> Look.fromColor Color.Green
      | Capturing -> Look.fromColor Color.Red

    box look |> withTitle $"[{focusNumber}] {title}" |> withInnerWidget innerWidget
