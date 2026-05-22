module Avatar

open System
open Spectre.Tui
open Keymap
open SpectreTuff.Widgets

type Model = { ConnectedUsers: string list }

type Msg = | NoOp

let init (sessionData: Session.Data) = {
  ConnectedUsers =
    match sessionData.ConnectedUsers with
    | null -> []
    | users -> users.Keys |> Seq.toList
}

let update _msg model =
  model, []

let private bindings: KeyBinding<Model, Msg> list = []

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) : IWidget =
  match model.ConnectedUsers with
  | [] -> ofString "  (no users connected)" :> IWidget
  | users ->
    let lines = users |> List.map (sprintf "  • %s") |> String.concat "\n"
    ofString lines :> IWidget
