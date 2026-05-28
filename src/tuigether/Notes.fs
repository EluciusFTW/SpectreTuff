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

// Freetext editing is single-writer: the user in Insert mode owns the lock and
// other users cannot enter Insert until it's released. LockedAt is refreshed on
// every debounced save so a crashed holder's lock expires after lockTtlMs.
type Lock = { Owner: string; LockedAt: int64 }

type Model = {
  NoteMode: NoteMode
  InputMode: InputMode
  FreetextContent: string
  FreetextSaveToken: int
  InsertActivityToken: int
  ListItems: NoteItem list
  ListIndex: int
  Lock: Lock option
  User: string
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
  | MaybeSaveFreetext of int
  | MaybeAutoExitInsert of int
  | RemoteStateLoaded of Session.NotesState option
  | StateSaved

let private freetextDebounceMs = 300
let private autoExitInsertMs = 30_000
let private lockTtlMs = 60_000L

let private nowMs () : int64 =
  DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let private isLockActive (now: int64) (lock: Lock option) =
  match lock with
  | Some l -> now - l.LockedAt <= lockTtlMs
  | None -> false

let private isLockedByOther (model: Model) =
  match model.Lock with
  | Some l when isLockActive (nowMs ()) (Some l) -> l.Owner <> model.User
  | _ -> false

let isHoldingLock (model: Model) =
  match model.InputMode, model.Lock with
  | Insert, Some l -> l.Owner = model.User
  | _ -> false

let private insertModeBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Escape "exit insert" ExitInsert
]

let private freetextNormalBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (CharKey 'i') (fun model ->
    match isLockedByOther model with
    | true ->
      let owner =
        model.Lock
        |> Option.map (fun l -> l.Owner)
        |> Option.defaultValue "another user"

      {
        Description = sprintf "locked by %s" owner
        Message = Some EnterInsert
      }
    | false -> {
        Description = "insert"
        Message = Some EnterInsert
      })
  KeyBinding.create 'm' "→ list" SwitchToList
]

let private listNormalBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" ListUp
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" ListDown
  KeyBinding.create 'a' "add" AddItem
  KeyBinding.create 'x' "delete" DeleteItem
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
        | 'x' -> Some DeleteItem
        | 'c' -> Some CopyItem
        | 'm' -> Some SwitchToFreetext
        | 'j' -> Some ListDown
        | 'k' -> Some ListUp
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

let init (client: FirebaseClient) (sessionId: string) (user: string) = {
  NoteMode = Freetext
  InputMode = Normal
  FreetextContent = ""
  FreetextSaveToken = 0
  InsertActivityToken = 0
  ListItems = []
  ListIndex = 0
  Lock = None
  User = user
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

// Debounce freetext writes: each typed character bumps a token and schedules a
// MaybeSaveFreetext for that token after a short idle delay. The actual save
// only fires if the token still matches the latest one — so a burst of fast
// keystrokes collapses into a single Firebase write at the end.
let private scheduleFreetextSave (token: int) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep freetextDebounceMs }) () (fun () -> MaybeSaveFreetext token)

// Auto-exit Insert mode after autoExitInsertMs of no typing activity. Every
// keystroke bumps the activity token and schedules a fresh check; only the
// scheduled check whose token still matches actually exits.
let private scheduleAutoExit (token: int) : Cmd<Msg> =
  Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep autoExitInsertMs }) () (fun () -> MaybeAutoExitInsert token)

let private saveLockCmd (model: Model) : Cmd<Msg> =
  match model.Lock with
  | Some lock ->
    Cmd.OfAsync.perform
      (fun () -> Firebase.Notes.saveLock model.Persistence.Client model.Persistence.SessionId lock.Owner lock.LockedAt)
      ()
      (fun () -> StateSaved)
  | None -> []

let private releaseLockCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Notes.releaseLock model.Persistence.Client model.Persistence.SessionId)
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
  | EnterInsert ->
    match isLockedByOther model with
    | true -> model, []
    | false ->
      let activityToken = model.InsertActivityToken + 1

      let lock = {
        Owner = model.User
        LockedAt = nowMs ()
      }

      let updated = {
        model with
            InputMode = Insert
            InsertActivityToken = activityToken
            Lock = Some lock
      }

      updated, Cmd.batch [ saveLockCmd updated; scheduleAutoExit activityToken ]
  | ExitInsert ->
    // Bump the token so any in-flight debounced save is cancelled, then flush
    // the current content immediately so other users see the final edit.
    let bumped = model.FreetextSaveToken + 1

    let wasFreetextInsert =
      match model.InputMode, model.NoteMode with
      | Insert, Freetext -> true
      | _ -> false

    let updated = {
      model with
          InputMode = Normal
          FreetextSaveToken = bumped
          Lock =
            match wasFreetextInsert with
            | true -> None
            | false -> model.Lock
    }

    let cmds =
      match wasFreetextInsert with
      | true -> [ saveFreetextCmd updated; releaseLockCmd updated ]
      | false -> []

    updated, Cmd.batch cmds
  | TypeChar c ->
    match model.InputMode with
    | AddingItem text ->
      {
        model with
            InputMode = AddingItem(text + string c)
      },
      []
    | _ ->
      let bumped = model.FreetextSaveToken + 1
      let activityToken = model.InsertActivityToken + 1

      let updated = {
        model with
            FreetextContent = model.FreetextContent + string c
            FreetextSaveToken = bumped
            InsertActivityToken = activityToken
      }

      updated, Cmd.batch [ scheduleFreetextSave bumped; scheduleAutoExit activityToken ]
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
      let bumped = model.FreetextSaveToken + 1
      let activityToken = model.InsertActivityToken + 1

      let updated = {
        model with
            FreetextContent =
              match text with
              | "" -> ""
              | _ -> text.[.. text.Length - 2]
            FreetextSaveToken = bumped
            InsertActivityToken = activityToken
      }

      updated, Cmd.batch [ scheduleFreetextSave bumped; scheduleAutoExit activityToken ]
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
      let bumped = model.FreetextSaveToken + 1
      let activityToken = model.InsertActivityToken + 1

      let updated = {
        model with
            FreetextContent = model.FreetextContent + "\n"
            FreetextSaveToken = bumped
            InsertActivityToken = activityToken
      }

      updated, Cmd.batch [ scheduleFreetextSave bumped; scheduleAutoExit activityToken ]
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

    // While the user is actively typing in freetext, ignore the freetext echo
    // from the remote — applying it would clobber characters typed since the
    // in-flight save was dispatched.
    let freetextContent =
      match model.InputMode with
      | Insert ->
        match model.NoteMode with
        | Freetext -> model.FreetextContent
        | List ->
          match isNull state.FreetextContent with
          | true -> ""
          | false -> state.FreetextContent
      | Normal
      | AddingItem _ ->
        match isNull state.FreetextContent with
        | true -> ""
        | false -> state.FreetextContent

    let remoteLock =
      match isNull state.LockOwner || state.LockOwner = "" with
      | true -> None
      | false ->
        Some {
          Owner = state.LockOwner
          LockedAt = state.LockedAt
        }

    {
      model with
          FreetextContent = freetextContent
          ListItems = listItems
          ListIndex = listIndex
          NoteMode =
            match state.NoteMode with
            | "List" -> List
            | _ -> Freetext
          Lock = remoteLock
    },
    []
  | RemoteStateLoaded None -> model, []
  | MaybeSaveFreetext token ->
    match token = model.FreetextSaveToken with
    | true ->
      // Refresh the lock timestamp on every save so the holder doesn't appear
      // stale to other clients while they're actively typing.
      let refreshedLock =
        match model.Lock with
        | Some l when l.Owner = model.User -> Some { l with LockedAt = nowMs () }
        | other -> other

      let updated = { model with Lock = refreshedLock }
      updated, Cmd.batch [ saveFreetextCmd updated; saveLockCmd updated ]
    | false -> model, []
  | MaybeAutoExitInsert token ->
    match model.InputMode = Insert && token = model.InsertActivityToken with
    | true -> model, Cmd.ofMsg ExitInsert
    | false -> model, []
  | StateSaved -> model, []

let subscriptions (model: Model) =
  Firebase.Notes.subscription model.Persistence.Client model.Persistence.SessionId RemoteStateLoaded

// List items render green, matching the Todo widget. The selected row inverts
// to black-on-green instead of the default list item's yellow-on-blue.
type private NotesListItem(text: string) =
  interface IListWidgetItem with
    member _.CreateText(isSelected) =
      let style =
        match isSelected with
        | true -> Style(Color.Black, Color.Green)
        | false -> Style(Color.Green)

      Text(LineExtensions.FromString(" • " + text, style))

let widget (model: Model) (isFocused: bool) : IWidget =
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
    let items = model.ListItems |> List.map (fun item -> NotesListItem item.Text)

    let listWidget =
      list items
      |> withSelectedIndex (
        match isFocused, items with
        | false, _
        | _, [] -> None
        | _ -> Some model.ListIndex
      )
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
