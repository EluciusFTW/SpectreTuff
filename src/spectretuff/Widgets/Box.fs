[<AutoOpen>]
module SpectreTuff.Widgets.Box

open Spectre.Console
open Spectre.Tui
open SpectreTuff.Look

let box (look: Look) =
  BoxWidget(toStyle look)

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
    | Unfocused -> fromColor Color.Grey
    | Focused -> fromColor Color.Green
    | Capturing -> fromColor Color.Red

  box look |> withTitle $"[{focusNumber}] {title}" |> withInnerWidget innerWidget
