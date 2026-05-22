module GlobalKeys

open System
open Keymap

type Msg =
  | PauseDrive
  | ResumeDrive
  | Teleport
  | NextDrive

let private bindings: KeyBinding<unit, Msg> list = [
  KeyBinding.create 'p' "pause" PauseDrive
  KeyBinding.create 'r' "resume" ResumeDrive
  KeyBinding.create 't' "teleport" Teleport
  KeyBinding.create 'n' "next drive" NextDrive
]

let handleKey (key: ConsoleKeyInfo) : Msg option =
  KeyBinding.handleKey bindings key ()

let keyMap: Spectre.Tui.App.IKeyMap = KeyBinding.toKeyMap bindings ()
