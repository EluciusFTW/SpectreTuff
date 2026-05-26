module Notes

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Widgets

type NoteMode =
  | Freetext
  | List

type InputMode =
  | Normal
  | Insert
  | AddingItem of string

type Persistence = {
  Client: FirebaseClient
  SessionId: string
}

type Model = {
  NoteMode: NoteMode
  InputMode: InputMode
  FreetextContent: string
  ListItems: string list
  ListIndex: int
  Persistence: Persistence
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
  | RemoteStateLoaded of Session.NotesState option
  | StateSaved

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

let private addingItemBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Enter "confirm" TypeNewLine
  KeyBinding.createSpecial ConsoleKey.Escape "cancel" ExitInsert
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match model.InputMode with
  | Insert
  | AddingItem _ ->
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
  match model.InputMode with
  | Insert
  | AddingItem _ -> true
  | Normal -> false

let keyMap (model: Model) =
  let bindings =
    match model.NoteMode, model.InputMode with
    | _, Insert -> insertModeBindings
    | _, AddingItem _ -> addingItemBindings
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

let init (client: FirebaseClient) (sessionId: string) = {
  NoteMode = Freetext
  InputMode = Normal
  FreetextContent = ""
  ListItems = []
  ListIndex = 0
  Persistence = {
    Client = client
    SessionId = sessionId
  }
}

let private toNotesState (model: Model) : Session.NotesState =
  let listItems =
    model.ListItems
    |> List.mapi (fun i item -> string i, item)
    |> dict
    |> Dictionary

  {
    FreetextContent = model.FreetextContent
    ListItems = listItems
    NoteMode =
      match model.NoteMode with
      | List -> "List"
      | _ -> "Freetext"
  }

let private saveCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Notes.save model.Persistence.Client model.Persistence.SessionId (toNotesState model))
    ()
    (fun () -> StateSaved)

let private withSave (model: Model) : Model * Cmd<Msg> =
  model, saveCmd model

let update msg model =
  match msg with
  | SwitchToFreetext ->
    {
      model with
          NoteMode = Freetext
          InputMode = Normal
    }
    |> withSave
  | SwitchToList ->
    {
      model with
          NoteMode = List
          InputMode = Normal
    }
    |> withSave
  | EnterInsert -> { model with InputMode = Insert }, []
  | ExitInsert -> { model with InputMode = Normal }, []
  | TypeChar c ->
    match model.InputMode with
    | AddingItem text ->
      {
        model with
            InputMode = AddingItem(text + string c)
      },
      []
    | _ ->
      {
        model with
            FreetextContent = model.FreetextContent + string c
      }
      |> withSave
  | TypeBackspace ->
    match model.InputMode with
    | AddingItem text ->
      {
        model with
            InputMode =
              AddingItem(
                match text with
                | "" -> ""
                | _ -> text.[.. text.Length - 2]
              )
      },
      []
    | _ ->
      let text = model.FreetextContent

      {
        model with
            FreetextContent =
              match text with
              | "" -> ""
              | _ -> text.[.. text.Length - 2]
      }
      |> withSave
  | TypeNewLine ->
    match model.InputMode with
    | AddingItem text ->
      let insertAt = min (model.ListIndex + 1) model.ListItems.Length

      let newText =
        match text.Trim() with
        | "" -> "New note"
        | s -> s

      let newItems = model.ListItems |> List.insertAt insertAt newText

      {
        model with
            ListItems = newItems
            ListIndex = insertAt
            InputMode = Normal
      }
      |> withSave
    | _ ->
      {
        model with
            FreetextContent = model.FreetextContent + "\n"
      }
      |> withSave
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
  | AddItem -> { model with InputMode = AddingItem "" }, []
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
      }
      |> withSave
  | CopyItem ->
    match model.ListItems with
    | [] -> ()
    | _ -> copyToClipboard model.ListItems.[model.ListIndex]

    model, []
  | RemoteStateLoaded(Some state) ->
    {
      model with
          FreetextContent =
            match isNull state.FreetextContent with
            | true -> ""
            | false -> state.FreetextContent
          ListItems =
            match isNull state.ListItems with
            | true -> []
            | false -> state.ListItems.Values |> Seq.toList
          NoteMode =
            match state.NoteMode with
            | "List" -> List
            | _ -> Freetext
    },
    []
  | RemoteStateLoaded None -> model, []
  | StateSaved -> model, []

let subscriptions (model: Model) =
  Firebase.Notes.subscription model.Persistence.Client model.Persistence.SessionId RemoteStateLoaded

let widget (model: Model) : IWidget =
  match model.NoteMode with
  | Freetext ->
    textBox model.FreetextContent
    |> withMode TextBoxMode.MultiLine
    |> (match model.InputMode with
        | Insert -> focused >> withCursorAtEnd
        | Normal
        | AddingItem _ -> unfocused)
    :> IWidget
  | List ->
    let items = model.ListItems |> List.map ListItem

    let listWidget =
      list items
      |> withSelectedIndex (
        match items with
        | [] -> None
        | _ -> Some model.ListIndex
      )
      |> withHighlightSymbol (LineExtensions.FromString("> ", Style Color.Green))
      |> wrapAround
      :> IWidget

    match model.InputMode with
    | AddingItem text ->
      { new IWidget with
          member _.Render(ctx) =
            ctx.Render(listWidget)

            let inputWidget =
              textBox text
              |> withMode TextBoxMode.SingleLine
              |> withPlaceholder "Enter item text…"
              |> focused
              |> withCursorAtEnd
              :> IWidget

            let boxedInput =
              box (Look.fromColor Color.Green)
              |> withTitle "New item"
              |> withInnerWidget inputWidget
              :> IWidget

            ctx.Render(popup 44 3 |> withPopupContent boxedInput :> IWidget)
      }
    | _ -> listWidget
