namespace SpectreTuff

open Spectre.Tui

module View =
  let shrink (rectangle: Rectangle) w h =
    rectangle.Inflate(Size(-1 * w, -1 * h))

  let getInner rectangle =
    shrink rectangle 1 1

  let padding left top right bottom =
    Padding(left, top, right, bottom)

  let uniformPadding size =
    Padding size

  let symmetricPadding horizontal vertical =
    Padding(horizontal, vertical)
