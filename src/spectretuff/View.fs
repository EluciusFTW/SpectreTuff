namespace SpectreTuff

open Spectre.Tui

module View =
  let shrink (rectangle: Rectangle) w h =
    rectangle.Inflate(Size(-1 * w, -1 * h))

  let getInner rectangle =
    shrink rectangle 1 1
