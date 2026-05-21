namespace SpectreTuff

open Spectre.Tui
open Spectre.Tui.App

module Input =

  let charPress (c: char) = KeyPress.For c
  let keyPress (key: Key) = KeyPress.For key

  let binding (presses: KeyPress list) =
    KeyBinding(Keys = ResizeArray presses)

  let charBinding (c: char) =
    KeyBinding(Keys = ResizeArray [charPress c])

  let keyBinding (key: Key) =
    KeyBinding(Keys = ResizeArray [keyPress key])

  let withHelp (description: string) (b: KeyBinding) =
    KeyBinding(Keys = b.Keys, Enabled = b.Enabled, Help = description, Order = b.Order)

  let enable (b: KeyBinding) =
    KeyBinding(Keys = b.Keys, Enabled = true, Help = b.Help, Order = b.Order)

  let disable (b: KeyBinding) =
    KeyBinding(Keys = b.Keys, Enabled = false, Help = b.Help, Order = b.Order)

  let withOrder (order: int) (b: KeyBinding) =
    KeyBinding(Keys = b.Keys, Enabled = b.Enabled, Help = b.Help, Order = order)
