module SessionList

open System
open Spectre.Tui
open Keymap
open SpectreTuff.Widgets

type Model = {
  Sessions: (string * Session.Data) list
  SelectedIndex: int
  Status: string
}

type Msg =
  | Up
  | Down
  | OpenSelected
  | CreateNew
  | SessionsLoaded of (string * Session.Data) list
  | SessionChanged of string * Session.Data
  | SessionRemoved of string
  | LoadError of string

let init () =
  {
    Sessions = []
    SelectedIndex = 0
    Status = "loading…"
  },
  []

let private sortSessions sessions =
  sessions
  |> List.sortByDescending (fun (_, data: Session.Data) -> data.StartedAt)

let update msg model =
  match msg with
  | Up ->
    {
      model with
          SelectedIndex = max 0 (model.SelectedIndex - 1)
    },
    []
  | Down ->
    {
      model with
          SelectedIndex = min (List.length model.Sessions - 1) (model.SelectedIndex + 1)
    },
    []
  | OpenSelected -> model, []
  | CreateNew -> model, []
  | SessionsLoaded sessions ->
    {
      model with
          Sessions = sortSessions sessions
          Status = "connected"
    },
    []
  | SessionChanged(id, data) when not (String.IsNullOrEmpty id) && not (isNull (data :> obj)) ->
    let sessions =
      model.Sessions
      |> List.filter (fun (k, _) -> k <> id)
      |> List.append [ id, data ]

    {
      model with
          Sessions = sortSessions sessions
    },
    []
  | SessionChanged _ -> model, []
  | SessionRemoved id ->
    {
      model with
          Sessions = model.Sessions |> List.filter (fun (k, _) -> k <> id)
    },
    []
  | LoadError e ->
    {
      model with
          Status = sprintf "error: %s" e
    },
    []

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" Down
  KeyBinding.createSpecial ConsoleKey.Enter "open" OpenSelected
  KeyBinding.create 'n' "new session" CreateNew
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) : IWidget =
  match model.Sessions with
  | [] -> ofString "No sessions yet. Press [n] to create one." :> IWidget
  | _ ->
    let items =
      model.Sessions
      |> List.choose (fun (_, data) ->
        match data :> obj with
        | null -> None
        | _ ->
          let userCount =
            match data.ConnectedUsers with
            | null -> 0
            | users -> users.Count

          let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm")

          Some(ListItem(sprintf "%-40s  %s  %d user(s)" data.Goal startedAt userCount)))

    list items |> selectedIndex model.SelectedIndex |> wrapAround :> IWidget
