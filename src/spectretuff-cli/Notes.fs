module Notes

open System
open System.Runtime.InteropServices
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff.Widgets

type NoteMode =
  | Freetext
  | List

type InputMode =
  | Normal
  | Insert

type Model = {
  NoteMode: NoteMode
  InputMode: InputMode
  FreetextContent: string
  ListItems: string list
  ListIndex: int
}

type Msg =
  | SwitchToFreetext
  | SwitchToList
  | EnterInsert
  | ExitInsert
  | TypeChar of char
  | TypeBackspace
  | TypeNewLine
  | ListUp
  | ListDown
  | AddItem
  | DeleteItem
  | CopyItem

let private insertModeBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Escape "exit insert" ExitInsert
]

let private freetextNormalBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.create 'i' "insert" EnterInsert
  KeyBinding.create 'm' "→ list" SwitchToList
]

let private listNormalBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" ListUp
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" ListDown
  KeyBinding.create 'a' "add" AddItem
  KeyBinding.create 'd' "delete" DeleteItem
  KeyBinding.create 'c' "copy" CopyItem
  KeyBinding.create 'm' "→ freetext" SwitchToFreetext
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match model.InputMode with
  | Insert ->
    match key.Key with
    | ConsoleKey.Escape -> Some ExitInsert
    | ConsoleKey.Backspace -> Some TypeBackspace
    | ConsoleKey.Enter -> Some TypeNewLine
    | _ when key.KeyChar <> '\000' -> Some(TypeChar key.KeyChar)
    | _ -> None
  | Normal ->
    match model.NoteMode with
    | Freetext ->
      match key.KeyChar with
      | 'i' -> Some EnterInsert
      | 'm' -> Some SwitchToList
      | _ -> None
    | List ->
      match key.Key with
      | ConsoleKey.UpArrow -> Some ListUp
      | ConsoleKey.DownArrow -> Some ListDown
      | _ ->
        match key.KeyChar with
        | 'a' -> Some AddItem
        | 'd' -> Some DeleteItem
        | 'c' -> Some CopyItem
        | 'm' -> Some SwitchToFreetext
        | _ -> None

let capturesInput (model: Model) =
  model.InputMode = Insert

let keyMap (model: Model) =
  let bindings =
    match model.NoteMode, model.InputMode with
    | _, Insert -> insertModeBindings
    | Freetext, Normal -> freetextNormalBindings
    | List, Normal -> listNormalBindings

  KeyBinding.toKeyMap bindings model

let private copyToClipboard (text: string) =
  try
    let psi = Diagnostics.ProcessStartInfo()
    psi.UseShellExecute <- false
    psi.RedirectStandardInput <- true

    match RuntimeInformation.IsOSPlatform OSPlatform.Windows, RuntimeInformation.IsOSPlatform OSPlatform.OSX with
    | true, _ ->
      psi.FileName <- "cmd"
      psi.Arguments <- "/c clip"
    | _, true -> psi.FileName <- "pbcopy"
    | _ ->
      psi.FileName <- "xclip"
      psi.Arguments <- "-selection clipboard"

    let proc = Diagnostics.Process.Start(psi)
    proc.StandardInput.Write(text)
    proc.StandardInput.Close()
    proc.WaitForExit()
  with _ ->
    match RuntimeInformation.IsOSPlatform OSPlatform.Linux with
    | true ->
      try
        let psi = Diagnostics.ProcessStartInfo("wl-copy")
        psi.UseShellExecute <- false
        psi.RedirectStandardInput <- true
        let proc = Diagnostics.Process.Start(psi)
        proc.StandardInput.Write(text)
        proc.StandardInput.Close()
        proc.WaitForExit()
      with _ ->
        ()
    | false -> ()

let init () = {
  NoteMode = Freetext
  InputMode = Normal
  FreetextContent = ""
  ListItems = [ "First note"; "Second note" ]
  ListIndex = 0
}

let update msg model =
  match msg with
  | SwitchToFreetext ->
    {
      model with
          NoteMode = Freetext
          InputMode = Normal
    },
    []
  | SwitchToList ->
    {
      model with
          NoteMode = List
          InputMode = Normal
    },
    []
  | EnterInsert -> { model with InputMode = Insert }, []
  | ExitInsert -> { model with InputMode = Normal }, []
  | TypeChar c ->
    {
      model with
          FreetextContent = model.FreetextContent + string c
    },
    []
  | TypeBackspace ->
    let text = model.FreetextContent

    {
      model with
          FreetextContent =
            match text with
            | "" -> ""
            | _ -> text.[.. text.Length - 2]
    },
    []
  | TypeNewLine ->
    {
      model with
          FreetextContent = model.FreetextContent + "\n"
    },
    []
  | ListUp ->
    let count = model.ListItems.Length

    match count with
    | 0 -> model, []
    | _ ->
      {
        model with
            ListIndex = (model.ListIndex - 1 + count) % count
      },
      []
  | ListDown ->
    let count = model.ListItems.Length

    match count with
    | 0 -> model, []
    | _ ->
      {
        model with
            ListIndex = (model.ListIndex + 1) % count
      },
      []
  | AddItem ->
    let insertAt = min (model.ListIndex + 1) model.ListItems.Length
    let newItems = model.ListItems |> List.insertAt insertAt "New note"

    {
      model with
          ListItems = newItems
          ListIndex = insertAt
    },
    []
  | DeleteItem ->
    match model.ListItems with
    | [] -> model, []
    | _ ->
      let newItems = model.ListItems |> List.removeAt model.ListIndex

      let newIndex =
        match newItems with
        | [] -> 0
        | _ -> min model.ListIndex (newItems.Length - 1)

      {
        model with
            ListItems = newItems
            ListIndex = newIndex
      },
      []
  | CopyItem ->
    match model.ListItems with
    | [] -> ()
    | _ -> copyToClipboard model.ListItems.[model.ListIndex]

    model, []

let widget (model: Model) : IWidget =
  match model.NoteMode with
  | Freetext ->
    textBox model.FreetextContent
    |> withMode TextBoxMode.MultiLine
    |> (match model.InputMode with
        | Insert -> focused >> withCursorAtEnd
        | Normal -> unfocused)
    :> IWidget
  | List ->
    let items = model.ListItems |> List.map ListItem

    list items
    |> withSelectedIndex (
      match items with
      | [] -> None
      | _ -> Some model.ListIndex
    )
    |> withHighlightSymbol (LineExtensions.FromString("> ", Style Color.Green))
    |> wrapAround
    :> IWidget
