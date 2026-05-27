namespace SpectreTuff

open Spectre.Tui
open Spectre.Tui.App

module Input =

  let charPress (c: char) =
    KeyPress.For c

  let keyPress (key: Key) =
    KeyPress.For key

  let binding (presses: KeyPress list) =
    KeyBinding(Keys = ResizeArray presses)

  let charBinding (c: char) =
    KeyBinding(Keys = ResizeArray [ charPress c ])

  let keyBinding (key: Key) =
    KeyBinding(Keys = ResizeArray [ keyPress key ])

  let private copy enabled help order keys =
    KeyBinding(Keys = keys, Enabled = enabled, Help = help, Order = order)

  let withHelp (description: string) (binding: KeyBinding) =
    binding.Keys |> copy binding.Enabled description binding.Order

  let enable (binding: KeyBinding) =
    binding.Keys |> copy true binding.Help binding.Order

  let disable (binding: KeyBinding) =
    binding.Keys |> copy false binding.Help binding.Order

  let withOrder (order: int) (binding: KeyBinding) =
    binding.Keys |> copy binding.Enabled binding.Help order
