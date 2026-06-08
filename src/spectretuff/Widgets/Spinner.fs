[<AutoOpen>]
module SpectreTuff.Widgets.Spinners

open Spectre.Tui

let spinner (kind: SpinnerKind) =
  let widget = SpinnerWidget()
  widget.Kind <- kind
  widget
