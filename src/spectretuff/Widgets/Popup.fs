[<AutoOpen>]
module SpectreTuff.Widgets.Popups

open Spectre.Tui

let popup (width: int) (height: int) =
  PopupWidget(Size(width, height))

let withPopupContent (content: IWidget) (popup: PopupWidget) =
  popup.Content <- content
  popup

let withoutBackdrop (popup: PopupWidget) =
  popup.Backdrop <- null
  popup
