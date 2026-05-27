module SessionList

open System
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

type InputMode =
  | Browsing
  | Naming of text: string * error: string option

type Model = {
  Sessions: (string * Session.Data) list
  SelectedIndex: int
  Status: string
  User: string
  Client: FirebaseClient
  InputMode: InputMode
  ConnectedUsers: Map<string, Set<string>>
}

type Msg =
  | Up
  | Down
  | OpenSelected
  | BeginNaming
  | TypeChar of char
  | TypeBackspace
  | ConfirmName
  | CancelNaming
  | DeleteSelected
  | FirebaseEvent of Firebase.SessionEvent
  | ConnectedUserChanged of sessionId: string * user: string
  | ConnectedUserRemoved of sessionId: string * user: string
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
    InputMode = Browsing
    ConnectedUsers = Map.empty
  },
  []

let private sortSessions sessions =
  sessions
  |> List.sortByDescending (fun (_, data: Session.Data) -> data.StartedAt)

let capturesInput (model: Model) =
  match model.InputMode with
  | Naming _ -> true
  | Browsing -> false

let private isDuplicateGoal (model: Model) (goal: string) =
  model.Sessions
  |> List.exists (fun (_, data) ->
    match data :> obj with
    | null -> false
    | _ ->
      not (isNull data.Goal)
      && String.Equals(data.Goal.Trim(), goal, StringComparison.OrdinalIgnoreCase))

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
  | BeginNaming ->
    {
      model with
          InputMode = Naming("", None)
    },
    [],
    None
  | TypeChar c ->
    match model.InputMode with
    | Naming(text, _) ->
      {
        model with
            InputMode = Naming(text + string c, None)
      },
      [],
      None
    | Browsing -> model, [], None
  | TypeBackspace ->
    match model.InputMode with
    | Naming(text, _) ->
      let trimmed =
        match text with
        | "" -> ""
        | _ -> text.[.. text.Length - 2]

      {
        model with
            InputMode = Naming(trimmed, None)
      },
      [],
      None
    | Browsing -> model, [], None
  | ConfirmName ->
    match model.InputMode with
    | Naming(text, _) ->
      let trimmed = text.Trim()

      match trimmed with
      | "" ->
        {
          model with
              InputMode = Naming(text, Some "Title required")
        },
        [],
        None
      | _ when isDuplicateGoal model trimmed ->
        {
          model with
              InputMode = Naming(text, Some "Title already used")
        },
        [],
        None
      | _ ->
        { model with InputMode = Browsing },
        Cmd.OfAsync.perform (fun () -> Firebase.Sessions.create model.Client model.User trimmed) () CreateCompleted,
        None
    | Browsing -> model, [], None
  | CancelNaming -> { model with InputMode = Browsing }, [], None
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
          ConnectedUsers = model.ConnectedUsers |> Map.remove id
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
  | ConnectedUserChanged(sessionId, user) ->
    let existing = model.ConnectedUsers |> Map.tryFind sessionId |> Option.defaultValue Set.empty

    {
      model with
          ConnectedUsers = model.ConnectedUsers |> Map.add sessionId (Set.add user existing)
    },
    [],
    None
  | ConnectedUserRemoved(sessionId, user) ->
    let existing = model.ConnectedUsers |> Map.tryFind sessionId |> Option.defaultValue Set.empty

    {
      model with
          ConnectedUsers = model.ConnectedUsers |> Map.add sessionId (Set.remove user existing)
    },
    [],
    None
  | CreateCompleted _ -> model, [], None
  | DeleteCompleted _ -> model, [], None

let subscriptions (model: Model) =
  let sessionsSub = Firebase.Sessions.subscription model.Client FirebaseEvent model

  let perSessionSubs =
    model.Sessions
    |> List.collect (fun (sessionId, _) ->
      Firebase.Users.subscription model.Client sessionId (fun ev ->
        match ev with
        | Firebase.UserChanged(user, _) -> ConnectedUserChanged(sessionId, user)
        | Firebase.UserRemoved user -> ConnectedUserRemoved(sessionId, user)))

  sessionsSub @ perSessionSubs

let private browsingBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" Down
  KeyBinding.createSpecial ConsoleKey.Enter "open" OpenSelected
  KeyBinding.create 'n' "new session" BeginNaming
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

let private namingBindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.Enter "confirm" ConfirmName
  KeyBinding.createSpecial ConsoleKey.Escape "cancel" CancelNaming
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  match model.InputMode with
  | Naming _ ->
    match key.Key with
    | ConsoleKey.Escape -> Some CancelNaming
    | ConsoleKey.Enter -> Some ConfirmName
    | ConsoleKey.Backspace -> Some TypeBackspace
    | _ when key.KeyChar <> '\000' -> Some(TypeChar key.KeyChar)
    | _ -> None
  | Browsing -> KeyBinding.handleKey browsingBindings key model

let keyMap (model: Model) =
  match model.InputMode with
  | Naming _ -> KeyBinding.toKeyMap namingBindings model
  | Browsing -> KeyBinding.toKeyMap browsingBindings model

type private StatusListItem(text: string, status: Session.Status) =
  interface IListWidgetItem with
    member _.CreateText(_isSelected) =
      let color =
        match status with
        | Session.Status.Created -> Color.Blue
        | Session.Status.Started -> Color.Green
        | Session.Status.Finished -> Color.Grey

      Text(LineExtensions.FromString(text, Style color))

let private popupInnerLayout =
  layout "popup-inner"
  |> splitHorizontally [| layout "input" |> withFixedSize (Some 1); layout "error" |]

let private formatConnected (users: Set<string>) =
  match Set.count users with
  | 0 -> ""
  | count when count <= 3 -> sprintf "[%d] %s" count (users |> String.concat ", ")
  | count -> sprintf "[%d]" count

let private formatDuration (millis: int64) =
  let totalSeconds = max 0L (millis / 1000L)
  let days = totalSeconds / 86400L
  let hours = (totalSeconds % 86400L) / 3600L
  let minutes = (totalSeconds % 3600L) / 60L
  let seconds = totalSeconds % 60L

  match days, hours, minutes with
  | 0L, 0L, 0L -> sprintf "%ds" seconds
  | 0L, 0L, _ -> sprintf "%dm" minutes
  | 0L, _, _ -> sprintf "%dh %dm" hours minutes
  | _ -> sprintf "%dd %dh" days hours

let widget (model: Model) : IWidget =
  let listWidget: IWidget =
    match model.Sessions with
    | [] -> ofString "No sessions yet. Press [n] to create one." :> IWidget
    | _ ->
      let nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

      let items =
        model.Sessions
        |> List.choose (fun (sessionId, data) ->
          match data :> obj with
          | null -> None
          | _ ->
            let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm")

            let status = Session.Status.fromString data.Status

            let duration =
              match data.WorkStartedAt with
              | 0L -> ""
              | workStartedAt -> formatDuration (nowMs - workStartedAt)

            let connected =
              model.ConnectedUsers
              |> Map.tryFind sessionId
              |> Option.defaultValue Set.empty
              |> formatConnected

            Some(StatusListItem(sprintf "%-40s  %s  %-8s  %s" data.Goal startedAt duration connected, status)))

      list items
      |> selectedIndex model.SelectedIndex
      |> withHighlightSymbol (LineExtensions.FromString("> ", Style Color.White))
      |> wrapAround
      :> IWidget

  match model.InputMode with
  | Browsing -> listWidget
  | Naming(text, error) ->
    { new IWidget with
        member _.Render(ctx) =
          ctx.Render(listWidget)

          let inputWidget =
            textBox text
            |> withMode TextBoxMode.SingleLine
            |> withPlaceholder "Session title…"
            |> focused
            |> withCursorAtEnd
            :> IWidget

          let popupContent =
            { new IWidget with
                member _.Render(innerCtx) =
                  let port = getPort innerCtx.Viewport popupInnerLayout
                  innerCtx.Render(inputWidget, port "input")

                  match error with
                  | Some msg ->
                    let errorWidget =
                      paragraph [ Text.line [ Text.styledSpan (Nullable(Style Color.Red)) msg ] ] :> IWidget

                    innerCtx.Render(errorWidget, port "error")
                  | None -> ()
            }

          let boxedInput =
            box (Look.fromColor Color.Green)
            |> withTitle "New session"
            |> withInnerWidget popupContent
            :> IWidget

          ctx.Render(popup 50 5 |> withPopupContent boxedInput :> IWidget)
    }
