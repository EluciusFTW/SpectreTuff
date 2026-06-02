namespace SpectreTuff.Widgets

open Spectre.Tui
open SpectreTuff

[<AutoOpen>]
module TextBoxes =
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

  let withReadOnly (enable: bool) (textBox: TextBoxWidget) =
    textBox.IsReadOnly <- enable
    textBox

  let withLook look (textBox: TextBoxWidget) =
    textBox.Style <- Look.toStyle look
    textBox

  let withPlaceholderLook look (textBox: TextBoxWidget) =
    textBox.PlaceholderStyle <- Look.toStyle look
    textBox

  let focused (textBox: TextBoxWidget) =
    textBox.IsFocused <- true
    textBox

  let unfocused (textBox: TextBoxWidget) =
    textBox.IsFocused <- false
    textBox
