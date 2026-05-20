module Keymap

open System
open Spectre.Tui
open SpectreTuff.Widgets

type KeyTrigger =
  | CharKey of char
  | SpecialKey of ConsoleKey

module KeyTrigger =
  let matches (key: ConsoleKeyInfo) = function
    | CharKey c -> key.KeyChar = c
    | SpecialKey k -> key.Key = k

  let display = function
    | CharKey c -> string c
    | SpecialKey ConsoleKey.UpArrow -> "↑"
    | SpecialKey ConsoleKey.DownArrow -> "↓"
    | SpecialKey ConsoleKey.LeftArrow -> "←"
    | SpecialKey ConsoleKey.RightArrow -> "→"
    | SpecialKey k -> k.ToString()

type KeyAction<'Msg> = {
  Description: string
  Message: 'Msg option
}

type KeyBinding<'Model, 'Msg> = {
  Trigger: KeyTrigger
  Action: 'Model -> KeyAction<'Msg>
}

module KeyBinding =

  let create key description message =
    { Trigger = CharKey key
      Action = fun _ -> { Description = description; Message = Some message } }

  let createSpecial key description message =
    { Trigger = SpecialKey key
      Action = fun _ -> { Description = description; Message = Some message } }

  let dynamic trigger action =
    { Trigger = trigger; Action = action }

  let handleKey (bindings: KeyBinding<'Model, 'Msg> list) (key: ConsoleKeyInfo) (model: 'Model) =
    bindings
    |> List.tryPick (fun b ->
      if KeyTrigger.matches key b.Trigger then (b.Action model).Message
      else None)

  let keys (bindings: KeyBinding<'Model, 'Msg> list) (model: 'Model) : KeyInfo list =
    bindings
    |> List.choose (fun b ->
      let action = b.Action model
      match action.Message with
      | Some _ -> Some { Key = KeyTrigger.display b.Trigger; Description = action.Description }
      | None -> None)
