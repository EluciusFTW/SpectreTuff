namespace SpectreTuff.Widgets

open Spectre.Tui

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

  let withStyle style (textBox: TextBoxWidget) =
    textBox.Style <- style
    textBox

  let withPlaceholderStyle style (textBox: TextBoxWidget) =
    textBox.PlaceholderStyle <- style
    textBox
