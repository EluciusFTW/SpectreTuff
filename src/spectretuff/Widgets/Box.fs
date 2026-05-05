namespace SpectreTuff.Widgets

open Spectre.Tui

[<AutoOpen>]
module Box =

  let box color =
    BoxWidget(Spectre.Console.Style(color, System.Nullable(), System.Nullable()))

  let withBoxTitle (title: BoxTitle) (box: BoxWidget) =
    box.Titles.Add title
    box

  let withTitle title =
    withBoxTitle (BoxTitle(TextLine(TextSpan title), TitlePosition.Top, Justify.Left))

  let withBorder (box: BoxWidget) border =
    box.Border <- border
    box
