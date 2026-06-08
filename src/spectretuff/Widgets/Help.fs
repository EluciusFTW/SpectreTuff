[<AutoOpen>]
module SpectreTuff.Widgets.Help

open Spectre.Tui
open Spectre.Tui.App
open SpectreTuff.Look

let help (keyMaps: IKeyMap seq) =
  HelpWidget(keyMaps |> Array.ofSeq)

let withSeparator (separator: string) (widget: HelpWidget) =
  widget.Separator <- separator
  widget

let withLook (look: Look) (widget: HelpWidget) =
  widget.Style <- toStyle look
  widget

let leftAligned (widget: HelpWidget) =
  widget.Alignment <- Justify.Left
  widget

let centered (widget: HelpWidget) =
  widget.Alignment <- Justify.Center
  widget

let rightAligned (widget: HelpWidget) =
  widget.Alignment <- Justify.Right
  widget
