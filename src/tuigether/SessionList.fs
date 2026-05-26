module SessionList

open System
open Elmish
open Firebase.Database
open Spectre.Tui
open Keymap
open SpectreTuff.Widgets

type Model = {
  Sessions: (string * Session.Data) list
  SelectedIndex: int
  Status: string
  User: string
  Client: FirebaseClient
}

type Msg =
  | Up
  | Down
  | OpenSelected
  | CreateNew
  | DeleteSelected
  | FirebaseEvent of Firebase.SessionEvent
  | CreateCompleted of Result<string, string>
  | DeleteCompleted of Result<unit, string>

type OutMsg = OpenSession of sessionId: string * data: Session.Data

let init (client: FirebaseClient) (user: string) () =
  {
    Sessions = []
    SelectedIndex = 0
    Status = "loading…"
    User = user
    Client = client
  },
  []

let private sortSessions sessions =
  sessions
  |> List.sortByDescending (fun (_, data: Session.Data) -> data.StartedAt)

let update msg model : Model * Cmd<Msg> * OutMsg option =
  match msg with
  | Up ->
    {
      model with
          SelectedIndex = max 0 (model.SelectedIndex - 1)
    },
    [],
    None
  | Down ->
    {
      model with
          SelectedIndex = min (List.length model.Sessions - 1) (model.SelectedIndex + 1)
    },
    [],
    None
  | OpenSelected ->
    match model.Sessions.IsEmpty with
    | true -> model, [], None
    | false ->
      let sessionId, sessionData = model.Sessions.[model.SelectedIndex]
      model, [], Some(OpenSession(sessionId, sessionData))
  | CreateNew ->
    model, Cmd.OfAsync.perform (fun () -> Firebase.Sessions.create model.Client model.User) () CreateCompleted, None
  | DeleteSelected ->
    match model.Sessions.IsEmpty with
    | true -> model, [], None
    | false ->
      let sessionId, _ = model.Sessions.[model.SelectedIndex]
      model, Cmd.OfAsync.perform (fun () -> Firebase.Sessions.delete model.Client sessionId) () DeleteCompleted, None
  | FirebaseEvent(Firebase.SessionsLoaded sessions) ->
    {
      model with
          Sessions = sortSessions sessions
          Status = "connected"
    },
    [],
    None
  | FirebaseEvent(Firebase.SessionChanged(id, data)) when not (String.IsNullOrEmpty id) && not (isNull (data :> obj)) ->
    let sessions =
      model.Sessions
      |> List.filter (fun (k, _) -> k <> id)
      |> List.append [ id, data ]

    {
      model with
          Sessions = sortSessions sessions
    },
    [],
    None
  | FirebaseEvent(Firebase.SessionChanged _) -> model, [], None
  | FirebaseEvent(Firebase.SessionRemoved id) ->
    {
      model with
          Sessions = model.Sessions |> List.filter (fun (k, _) -> k <> id)
    },
    [],
    None
  | FirebaseEvent(Firebase.ConnectionError e) ->
    {
      model with
          Status = sprintf "error: %s" e
    },
    [],
    None
  | CreateCompleted _ -> model, [], None
  | DeleteCompleted _ -> model, [], None

let subscriptions (model: Model) =
  Firebase.Sessions.subscription model.Client FirebaseEvent model

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" Down
  KeyBinding.createSpecial ConsoleKey.Enter "open" OpenSelected
  KeyBinding.create 'n' "new session" CreateNew
  KeyBinding.dynamic (SpecialKey ConsoleKey.Delete) (fun model ->
    let canDelete =
      not model.Sessions.IsEmpty
      && model.SelectedIndex >= 0
      && model.SelectedIndex < model.Sessions.Length
      && (let _, data = model.Sessions.[model.SelectedIndex]
          not (isNull (data :> obj)) && data.Creator = model.User)

    {
      Description = "delete session"
      Message = if canDelete then Some DeleteSelected else None
    })
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
          let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm")

          Some(ListItem(sprintf "%-40s  %s" data.Goal startedAt)))

    list items |> selectedIndex model.SelectedIndex |> wrapAround :> IWidget
