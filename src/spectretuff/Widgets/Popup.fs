namespace SpectreTuff.Widgets

open Spectre.Tui

[<AutoOpen>]
module Popups =
  let popup (width: int) (height: int) =
    PopupWidget(Size(width, height))

  let withPopupContent (content: IWidget) (popup: PopupWidget) =
    popup.Content <- content
    popup

  let withoutBackdrop (popup: PopupWidget) =
    popup.Backdrop <- null
    popup
