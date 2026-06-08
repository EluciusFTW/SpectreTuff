[<AutoOpen>]
module SpectreTuff.Widgets.TextBoxes

open Spectre.Tui
open SpectreTuff.Look

let textBox (text: string) =
  let widget = TextBoxWidget()
  widget.Text <- text
  widget

let withPlaceholder (placeholder: string) (textBox: TextBoxWidget) =
  textBox.Placeholder <- placeholder
  textBox

let withMode mode (textBox: TextBoxWidget) =
  textBox.Mode <- mode
  textBox

let withMaxLength (length: int option) (textBox: TextBoxWidget) =
  textBox.MaxLength <- Option.toNullable length
  textBox

let withPasswordChar (character: char option) (textBox: TextBoxWidget) =
  textBox.PasswordChar <- Option.toNullable character
  textBox

let readOnly (textBox: TextBoxWidget) =
  textBox.IsReadOnly <- true
  textBox

let editable (textBox: TextBoxWidget) =
  textBox.IsReadOnly <- false
  textBox

let withLook look (textBox: TextBoxWidget) =
  textBox.Style <- toStyle look
  textBox

let withPlaceholderLook look (textBox: TextBoxWidget) =
  textBox.PlaceholderStyle <- toStyle look
  textBox

let focused (textBox: TextBoxWidget) =
  textBox.IsFocused <- true
  textBox

let unfocused (textBox: TextBoxWidget) =
  textBox.IsFocused <- false
  textBox

let withCursorAtEnd (textBox: TextBoxWidget) =
  textBox.MoveToEnd()
  textBox

let withCursorAtStart (textBox: TextBoxWidget) =
  textBox.MoveToStart()
  textBox
