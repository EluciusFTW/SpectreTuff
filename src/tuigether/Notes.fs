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

// List items carry their Firebase push-ID so deletes target a stable key and
// concurrent multi-user edits do not collide.
type NoteItem = { Id: string; Text: string }

type Model = {
  NoteMode: NoteMode
  InputMode: InputMode
  FreetextContent: string
  ListItems: NoteItem list
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

let private noteModeString (mode: NoteMode) =
  match mode with
  | List -> "List"
  | Freetext -> "Freetext"

let private saveFreetextCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Notes.saveFreetext model.Persistence.Client model.Persistence.SessionId model.FreetextContent)
    ()
    (fun () -> StateSaved)

let private saveNoteModeCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () ->
      Firebase.Notes.saveNoteMode model.Persistence.Client model.Persistence.SessionId (noteModeString model.NoteMode))
    ()
    (fun () -> StateSaved)

let private addItemCmd (model: Model) (item: NoteItem) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Notes.addItem model.Persistence.Client model.Persistence.SessionId item.Id item.Text)
    ()
    (fun () -> StateSaved)

let private deleteItemCmd (model: Model) (itemId: string) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Notes.deleteItem model.Persistence.Client model.Persistence.SessionId itemId)
    ()
    (fun () -> StateSaved)

let update msg model =
  match msg with
  | SwitchToFreetext ->
    let updated = {
      model with
          NoteMode = Freetext
          InputMode = Normal
    }

    updated, saveNoteModeCmd updated
  | SwitchToList ->
    let updated = {
      model with
          NoteMode = List
          InputMode = Normal
    }

    updated, saveNoteModeCmd updated
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
      let updated = {
        model with
            FreetextContent = model.FreetextContent + string c
      }

      updated, saveFreetextCmd updated
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

      let updated = {
        model with
            FreetextContent =
              match text with
              | "" -> ""
              | _ -> text.[.. text.Length - 2]
      }

      updated, saveFreetextCmd updated
  | TypeNewLine ->
    match model.InputMode with
    | AddingItem text ->
      let newText =
        match text.Trim() with
        | "" -> "New note"
        | s -> s

      // Push IDs are chronologically sortable, so new items always append.
      let newItem = {
        Id = Firebase.PushId.generate ()
        Text = newText
      }

      let newItems = model.ListItems @ [ newItem ]

      let updated = {
        model with
            ListItems = newItems
            ListIndex = newItems.Length - 1
            InputMode = Normal
      }

      updated, addItemCmd updated newItem
    | _ ->
      let updated = {
        model with
            FreetextContent = model.FreetextContent + "\n"
      }

      updated, saveFreetextCmd updated
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
      let removed = model.ListItems.[model.ListIndex]
      let newItems = model.ListItems |> List.removeAt model.ListIndex

      let newIndex =
        match newItems with
        | [] -> 0
        | _ -> min model.ListIndex (newItems.Length - 1)

      let updated = {
        model with
            ListItems = newItems
            ListIndex = newIndex
      }

      updated, deleteItemCmd updated removed.Id
  | CopyItem ->
    match model.ListItems with
    | [] -> ()
    | _ -> copyToClipboard model.ListItems.[model.ListIndex].Text

    model, []
  | RemoteStateLoaded(Some state) ->
    let listItems =
      match isNull state.ListItems with
      | true -> []
      | false ->
        state.ListItems
        |> Seq.sortBy (fun kvp -> kvp.Key)
        |> Seq.map (fun kvp -> { Id = kvp.Key; Text = kvp.Value })
        |> Seq.toList

    let listIndex =
      match listItems with
      | [] -> 0
      | _ -> model.ListIndex |> max 0 |> min (listItems.Length - 1)

    {
      model with
          FreetextContent =
            match isNull state.FreetextContent with
            | true -> ""
            | false -> state.FreetextContent
          ListItems = listItems
          ListIndex = listIndex
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
    let items = model.ListItems |> List.map (fun item -> ListItem item.Text)

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
