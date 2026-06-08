[<AutoOpen>]
module SpectreTuff.Widgets.ProgressBars

open Spectre.Tui

let progressBar (value: float) (maximum: float) =
  let widget = ProgressBarWidget()
  widget.Value <- value
  widget.Max <- maximum
  widget

let withFill fill (progressBar: ProgressBarWidget) =
  progressBar.Fill <- fill
  progressBar

let withLabel label (progressBar: ProgressBarWidget) =
  progressBar.Label <- label
  progressBar.ShowLabel <- true
  progressBar

let showLabel (progressBar: ProgressBarWidget) =
  progressBar.ShowLabel <- true
  progressBar

let hideLabel (progressBar: ProgressBarWidget) =
  progressBar.ShowLabel <- false
  progressBar

let withForeground (brush: ProgressBarBrush option) (progressBar: ProgressBarWidget) =
  progressBar.Foreground <- Option.toObj brush
  progressBar

let withBackground (brush: ProgressBarBrush option) (progressBar: ProgressBarWidget) =
  progressBar.Background <- Option.toObj brush
  progressBar
