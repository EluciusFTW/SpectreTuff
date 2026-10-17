module SpectreTuff.Keymap

open System
open Spectre.Tui

type KeyTrigger =
  | CharKey of char
  | SpecialKey of ConsoleKey

module KeyTrigger =
  let matches (key: ConsoleKeyInfo) =
    function
    | CharKey c -> key.KeyChar = c
    | SpecialKey k -> key.Key = k

  let toKeyPress =
    function
    | CharKey c -> Spectre.Tui.App.KeyPress.For c
    | SpecialKey ConsoleKey.UpArrow -> Spectre.Tui.App.KeyPress.For Key.Up
    | SpecialKey ConsoleKey.DownArrow -> Spectre.Tui.App.KeyPress.For Key.Down
    | SpecialKey ConsoleKey.LeftArrow -> Spectre.Tui.App.KeyPress.For Key.Left
    | SpecialKey ConsoleKey.RightArrow -> Spectre.Tui.App.KeyPress.For Key.Right
    | SpecialKey ConsoleKey.Enter -> Spectre.Tui.App.KeyPress.For Key.Enter
    | SpecialKey ConsoleKey.Escape -> Spectre.Tui.App.KeyPress.For Key.Escape
    | SpecialKey ConsoleKey.Backspace -> Spectre.Tui.App.KeyPress.For Key.Backspace
    | SpecialKey ConsoleKey.Tab -> Spectre.Tui.App.KeyPress.For Key.Tab
    | SpecialKey ConsoleKey.Delete -> Spectre.Tui.App.KeyPress.For Key.Delete
    | SpecialKey ConsoleKey.Home -> Spectre.Tui.App.KeyPress.For Key.Home
    | SpecialKey ConsoleKey.End -> Spectre.Tui.App.KeyPress.For Key.End
    | SpecialKey ConsoleKey.PageUp -> Spectre.Tui.App.KeyPress.For Key.PageUp
    | SpecialKey ConsoleKey.PageDown -> Spectre.Tui.App.KeyPress.For Key.PageDown
    | SpecialKey ConsoleKey.Spacebar -> Spectre.Tui.App.KeyPress.For Key.Space
    | SpecialKey _ -> Spectre.Tui.App.KeyPress.For Key.None

  // Names keys the way Spectre.Tui's help bar does, for key hints spelled out
  // inside a dialog — except for the casing of character keys, which the footer
  // uppercases. Matching is case-sensitive (see `matches`), so a binding on 's'
  // shown as [S] advertises a key that does nothing; the case as bound wins.
  let displayName =
    function
    | CharKey c -> string c
    | SpecialKey ConsoleKey.UpArrow -> "↑"
    | SpecialKey ConsoleKey.DownArrow -> "↓"
    | SpecialKey ConsoleKey.LeftArrow -> "←"
    | SpecialKey ConsoleKey.RightArrow -> "→"
    | SpecialKey ConsoleKey.Enter -> "Enter"
    | SpecialKey ConsoleKey.Escape -> "Esc"
    | SpecialKey ConsoleKey.Tab -> "Tab"
    | SpecialKey ConsoleKey.Spacebar -> "Space"
    | SpecialKey ConsoleKey.Backspace -> "Backspace"
    | SpecialKey ConsoleKey.Delete -> "Del"
    | SpecialKey ConsoleKey.Insert -> "Ins"
    | SpecialKey ConsoleKey.Home -> "Home"
    | SpecialKey ConsoleKey.End -> "End"
    | SpecialKey ConsoleKey.PageUp -> "PgUp"
    | SpecialKey ConsoleKey.PageDown -> "PgDn"
    | SpecialKey k -> string k

type KeyAction<'Msg> = {
  Description: string
  Message: 'Msg option
}

type KeyBinding<'Model, 'Msg> = {
  // Usually one key, but a binding may carry aliases — Enter next to Esc on a
  // dismiss-only dialog, say. The help bar renders them as one `[Enter/Esc]`
  // entry, so an alias is documented rather than a hidden extra.
  Triggers: KeyTrigger list
  Action: 'Model -> KeyAction<'Msg>
}

module KeyBinding =

  let create key description message = {
    Triggers = [ CharKey key ]
    Action =
      fun _ -> {
        Description = description
        Message = Some message
      }
  }

  let createSpecial key description message = {
    Triggers = [ SpecialKey key ]
    Action =
      fun _ -> {
        Description = description
        Message = Some message
      }
  }

  let dynamic trigger action = {
    Triggers = [ trigger ]
    Action = action
  }

  // Adds an equivalent key to an existing binding: same message, one help entry.
  let orKey trigger binding = {
    binding with
        Triggers = binding.Triggers @ [ trigger ]
  }

  // The enabled bindings as (key label, description) pairs, for dialogs that spell
  // their choices out in the body instead of leaving them to the footer help bar.
  let helpEntries (bindings: KeyBinding<'Model, 'Msg> list) (model: 'Model) =
    bindings
    |> List.choose (fun b ->
      let action = b.Action model

      match action.Message with
      | Some _ -> Some(String.Join("/", b.Triggers |> List.map KeyTrigger.displayName), action.Description)
      | None -> None)

  let handleKey (bindings: KeyBinding<'Model, 'Msg> list) (key: ConsoleKeyInfo) (model: 'Model) =
    bindings
    |> List.tryPick (fun b ->
      if b.Triggers |> List.exists (KeyTrigger.matches key) then
        (b.Action model).Message
      else
        None)

  let toKeyMap (bindings: KeyBinding<'Model, 'Msg> list) (model: 'Model) : Spectre.Tui.App.IKeyMap = {
    new Spectre.Tui.App.IKeyMap with
      member _.Help() =
        bindings
        |> Seq.choose (fun b ->
          let action = b.Action model

          match action.Message with
          | Some _ ->
            Some(
              Spectre.Tui.App.KeyBinding(
                Keys = ResizeArray(b.Triggers |> List.map KeyTrigger.toKeyPress),
                Help = action.Description
              )
            )
          | None -> None)
  }
