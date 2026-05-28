module GlobalKeys

open System
open Keymap

type Msg =
  | StageDrive
  | FastForward

let private bindings (stageHelp: string) (canFastForward: bool) : KeyBinding<unit, Msg> list = [
  KeyBinding.create 's' stageHelp StageDrive
  KeyBinding.dynamic (CharKey 'f') (fun _ -> {
    Description = "fast-forward"
    Message =
      match canFastForward with
      | true -> Some FastForward
      | false -> None
  })
]

let handleKey (canFastForward: bool) (key: ConsoleKeyInfo) : Msg option =
  KeyBinding.handleKey (bindings "" canFastForward) key ()

let keyMap (stageHelp: string) (canFastForward: bool) : Spectre.Tui.App.IKeyMap =
  KeyBinding.toKeyMap (bindings stageHelp canFastForward) ()
