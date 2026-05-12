namespace SpectreTuff.Widgets

open Spectre.Tui

[<AutoOpen>]
module Spinners =
  let spinner (kind: SpinnerKind) =
    let widget = SpinnerWidget()
    widget.Kind <- kind
    widget
