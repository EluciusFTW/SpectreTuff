namespace SpectreTuff.Widgets

open Spectre.Tui

[<AutoOpen>]
module Box =

  let box color =
    BoxWidget(Spectre.Console.Style(color, System.Nullable(), System.Nullable()))

  let withBorder (box: BoxWidget) border =
    box.Border <- border
    box
