namespace SpectreTuff.Widgets

open Spectre.Tui
open SpectreTuff

[<AutoOpen>]
module KeyMap =

  type KeyInfo = { Key: string; Description: string }

  let keyInfo key description = { Key = key; Description = description }

  let showKeys (keys: KeyInfo list) =
    let inner =
      keys
      |> List.map (fun k -> $"  [{k.Key}] {k.Description}")
      |> String.concat "\n"
      |> textBox
      |> withMode TextBoxMode.MultiLine
      |> withReadOnly true
    box Look.empty
    |> withTitle "keymaps"
    |> withInnerWidget inner
