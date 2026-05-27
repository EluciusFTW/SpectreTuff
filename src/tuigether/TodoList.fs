module TodoList

open System
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Widgets

type InputMode =
  | Normal
  | AddingItem of string

type Persistence = {
  Client: FirebaseClient
  SessionId: string
}

// Items carry their Firebase push-ID so toggles and deletes target a stable key
// and concurrent multi-user edits do not collide.
type TodoItem = {
  Id: string
  Text: string
  Completed: bool
}

type Model = {
  InputMode: InputMode
  Items: TodoItem list
  SelectedIndex: int
  Persistence: Persistence
}

type Msg =
  | Up
  | Down
  | StartAdd
  | TypeChar of char
  | TypeBackspace
  | ConfirmAdd
  | CancelAdd
  | Toggle
  | Delete
  | MoveUp
  | MoveDown
  | RemoteStateLoaded of Session.TodoState option
  | StateSaved

let private normalBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" Down
  KeyBinding.create 'a' "add" StartAdd
  KeyBinding.create ' ' "toggle" Toggle
  KeyBinding.create 'x' "delete" Delete
  KeyBinding.create 'u' "move up" MoveUp
  KeyBinding.create 'd' "move down" MoveDown
]

let private addingItemBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Enter "confirm" ConfirmAdd
  KeyBinding.createSpecial ConsoleKey.Escape "cancel" CancelAdd
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match model.InputMode with
  | AddingItem _ ->
    match key.Key with
    | ConsoleKey.Escape -> Some CancelAdd
    | ConsoleKey.Backspace -> Some TypeBackspace
    | ConsoleKey.Enter -> Some ConfirmAdd
    | _ when key.KeyChar <> '\000' -> Some(TypeChar key.KeyChar)
    | _ -> None
  | Normal ->
    match key.Key with
    | ConsoleKey.UpArrow -> Some Up
    | ConsoleKey.DownArrow -> Some Down
    | _ ->
      match key.KeyChar with
      | 'a' -> Some StartAdd
      | ' ' -> Some Toggle
      | 'x' -> Some Delete
      | 'u' -> Some MoveUp
      | 'd' -> Some MoveDown
      | _ -> None

let capturesInput (model: Model) =
  match model.InputMode with
  | AddingItem _ -> true
  | Normal -> false

let keyMap (model: Model) =
  let bindings =
    match model.InputMode with
    | AddingItem _ -> addingItemBindings
    | Normal -> normalBindings

  KeyBinding.toKeyMap bindings model

let init (client: FirebaseClient) (sessionId: string) = {
  InputMode = Normal
  Items = []
  SelectedIndex = 0
  Persistence = {
    Client = client
    SessionId = sessionId
  }
}

let private addItemCmd (model: Model) (item: TodoItem) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Todo.addItem model.Persistence.Client model.Persistence.SessionId item.Id item.Text)
    ()
    (fun () -> StateSaved)

let private setCompletedCmd (model: Model) (itemId: string) (completed: bool) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Todo.setCompleted model.Persistence.Client model.Persistence.SessionId itemId completed)
    ()
    (fun () -> StateSaved)

let private deleteItemCmd (model: Model) (itemId: string) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Todo.deleteItem model.Persistence.Client model.Persistence.SessionId itemId)
    ()
    (fun () -> StateSaved)

let private setItemCmd (model: Model) (item: TodoItem) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () ->
      Firebase.Todo.setItem model.Persistence.Client model.Persistence.SessionId item.Id item.Text item.Completed)
    ()
    (fun () -> StateSaved)

// Display order is the push-ID key order, so reordering swaps the two adjacent
// items' content between their fixed key slots — the keys (and thus the sort
// order on reload) stay put while the visible text/checkbox moves.
let private swapAdjacent (model: Model) (topIndex: int) =
  let top = model.Items.[topIndex]
  let bottom = model.Items.[topIndex + 1]

  let newTop = {
    top with
        Text = bottom.Text
        Completed = bottom.Completed
  }

  let newBottom = {
    bottom with
        Text = top.Text
        Completed = top.Completed
  }

  let newItems =
    model.Items
    |> List.mapi (fun idx it ->
      match idx with
      | _ when idx = topIndex -> newTop
      | _ when idx = topIndex + 1 -> newBottom
      | _ -> it)

  newItems, newTop, newBottom

let update msg model =
  match msg with
  | Up ->
    let count = model.Items.Length

    match count with
    | 0 -> model, []
    | _ ->
      {
        model with
            SelectedIndex = (model.SelectedIndex - 1 + count) % count
      },
      []
  | Down ->
    let count = model.Items.Length

    match count with
    | 0 -> model, []
    | _ ->
      {
        model with
            SelectedIndex = (model.SelectedIndex + 1) % count
      },
      []
  | StartAdd -> { model with InputMode = AddingItem "" }, []
  | TypeChar c ->
    match model.InputMode with
    | AddingItem text ->
      {
        model with
            InputMode = AddingItem(text + string c)
      },
      []
    | Normal -> model, []
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
    | Normal -> model, []
  | ConfirmAdd ->
    match model.InputMode with
    | AddingItem text ->
      let newText =
        match text.Trim() with
        | "" -> "New todo"
        | s -> s

      // Push IDs are chronologically sortable, so new items always append.
      let newItem = {
        Id = Firebase.PushId.generate ()
        Text = newText
        Completed = false
      }

      let newItems = model.Items @ [ newItem ]

      let updated = {
        model with
            Items = newItems
            SelectedIndex = newItems.Length - 1
            InputMode = Normal
      }

      updated, addItemCmd updated newItem
    | Normal -> model, []
  | CancelAdd -> { model with InputMode = Normal }, []
  | Toggle ->
    match model.Items with
    | [] -> model, []
    | _ ->
      let item = model.Items.[model.SelectedIndex]

      let toggled = {
        item with
            Completed = not item.Completed
      }

      let newItems =
        model.Items
        |> List.mapi (fun i it ->
          match i = model.SelectedIndex with
          | true -> toggled
          | false -> it)

      { model with Items = newItems }, setCompletedCmd model toggled.Id toggled.Completed
  | Delete ->
    match model.Items with
    | [] -> model, []
    | _ ->
      let removed = model.Items.[model.SelectedIndex]
      let newItems = model.Items |> List.removeAt model.SelectedIndex

      let newIndex =
        match newItems with
        | [] -> 0
        | _ -> min model.SelectedIndex (newItems.Length - 1)

      let updated = {
        model with
            Items = newItems
            SelectedIndex = newIndex
      }

      updated, deleteItemCmd updated removed.Id
  | MoveUp ->
    match model.SelectedIndex with
    | i when i > 0 ->
      let newItems, newTop, newBottom = swapAdjacent model (i - 1)

      let updated = {
        model with
            Items = newItems
            SelectedIndex = i - 1
      }

      updated, Cmd.batch [ setItemCmd updated newTop; setItemCmd updated newBottom ]
    | _ -> model, []
  | MoveDown ->
    match model.SelectedIndex with
    | i when i < model.Items.Length - 1 ->
      let newItems, newTop, newBottom = swapAdjacent model i

      let updated = {
        model with
            Items = newItems
            SelectedIndex = i + 1
      }

      updated, Cmd.batch [ setItemCmd updated newTop; setItemCmd updated newBottom ]
    | _ -> model, []
  | RemoteStateLoaded(Some state) ->
    let items =
      match isNull state.Items with
      | true -> []
      | false ->
        state.Items
        |> Seq.sortBy (fun kvp -> kvp.Key)
        |> Seq.map (fun kvp -> {
          Id = kvp.Key
          Text = kvp.Value.Text
          Completed = kvp.Value.Completed
        })
        |> Seq.toList

    let selectedIndex =
      match items with
      | [] -> 0
      | _ -> model.SelectedIndex |> max 0 |> min (items.Length - 1)

    {
      model with
          Items = items
          SelectedIndex = selectedIndex
    },
    []
  | RemoteStateLoaded None -> model, []
  | StateSaved -> model, []

let subscriptions (model: Model) =
  Firebase.Todo.subscription model.Persistence.Client model.Persistence.SessionId RemoteStateLoaded

// Completed items render grey, still-to-do items green. The selected row inverts
// to black-on-(state colour) — legible and calm, instead of the default list
// item's bright yellow-on-blue.
type private TodoListItem(text: string, completed: bool) =
  interface IListWidgetItem with
    member _.CreateText(isSelected) =
      let stateColor =
        match completed with
        | true -> Color.Grey
        | false -> Color.Green

      let style =
        match isSelected with
        | true -> Style(Color.Black, stateColor)
        | false -> Style(stateColor)

      Text(LineExtensions.FromString(text, style))

let widget (model: Model) (isFocused: bool) : IWidget =
  let items =
    model.Items
    |> List.map (fun item ->
      let checkbox =
        match item.Completed with
        | true -> "[x] "
        | false -> "[ ] "

      TodoListItem(checkbox + item.Text, item.Completed))

  let listWidget =
    list items
    |> withSelectedIndex (
      match isFocused, items with
      | false, _
      | _, [] -> None
      | _ -> Some model.SelectedIndex
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
  | Normal -> listWidget
