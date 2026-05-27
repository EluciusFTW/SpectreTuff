namespace SpectreTuff

open Spectre.Tui

module View =
  let shrink (rectangle: Rectangle) width height =
    rectangle.Inflate(Size(-1 * width, -1 * height))

  let getInner rectangle =
    shrink rectangle 1 1

  let padding left top right bottom =
    Padding(left, top, right, bottom)

  let uniformPadding size =
    Padding size

  let symmetricPadding horizontal vertical =
    Padding(horizontal, vertical)

  let padWith (padding: Padding) (rectangle: Rectangle) =
    rectangle.Pad(padding)

  let center width height (rectangle: Rectangle) =
    rectangle.Center(width, height)

  let offset offsetX offsetY (rectangle: Rectangle) =
    rectangle.Offset(offsetX, offsetY)

  let intersect (other: Rectangle) (rectangle: Rectangle) =
    rectangle.Intersect(other)

  let boundingBox (other: Rectangle) (rectangle: Rectangle) =
    rectangle.Union(other)

  let intersectAll (rectangles: Rectangle seq) =
    Seq.reduce intersect rectangles

  let boundingBoxOf (rectangles: Rectangle seq) =
    Seq.reduce boundingBox rectangles
