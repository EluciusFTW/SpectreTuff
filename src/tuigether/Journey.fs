module Journey

open System
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

let private creatureByName (name: string) =
  match String.IsNullOrWhiteSpace(name) with
  | true -> library.[Random.Shared.Next(library.Length)]
  | false ->
    library
    |> List.tryFind (fun c -> String.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
    |> Option.defaultWith (fun () -> library.[Random.Shared.Next(library.Length)])

let resolveCreature () =
  creatureByName (Environment.GetEnvironmentVariable("TUIGETHER_AVATAR"))

let resolveName () =
  (resolveCreature ()).Name

type User = { Name: string; Creature: Creature }

type Persistence = {
  Client: FirebaseClient
  SessionId: string
}

type Model = {
  Users: User list
  ActiveDriver: User option
  CurrentUser: User
  Timer: Timer.Model
  Persistence: Persistence
}

type Msg =
  | UpdateSession of Session.Data
  | RemoteUserChanged of user: string * presence: Session.UserPresence
  | RemoteUserRemoved of user: string
  | SetActiveDriver of string option
  | ActiveDriverSaved
  | SwitchDriver
  | TimerMsg of Timer.Msg

let private upsertUser (userName: string) (presence: Session.UserPresence) (model: Model) : Model =
  let avatarName: string =
    match isNull (presence :> obj) with
    | true -> null
    | false -> presence.Avatar

  let updated =
    match userName = model.CurrentUser.Name with
    | true -> model.CurrentUser
    | false -> {
        Name = userName
        Creature = creatureByName avatarName
      }

  let users =
    match model.Users |> List.exists (fun u -> u.Name = userName) with
    | true -> model.Users |> List.map (fun u -> if u.Name = userName then updated else u)
    | false -> model.Users @ [ updated ]

  {
    model with
        Users = users
        ActiveDriver =
          match model.ActiveDriver with
          | None -> None
          | Some d when d.Name = userName -> Some updated
          | other -> other
  }

let private removeUser (userName: string) (model: Model) : Model =
  match userName = model.CurrentUser.Name with
  | true -> model
  | false -> {
      model with
          Users = model.Users |> List.filter (fun u -> u.Name <> userName)
          ActiveDriver =
            match model.ActiveDriver with
            | Some d when d.Name = userName -> None
            | other -> other
    }

let init (client: FirebaseClient) (sessionId: string) (currentUser: string) (avatarName: string) (data: Session.Data) =
  let myCreature = creatureByName avatarName

  let me = {
    Name = currentUser
    Creature = myCreature
  }

  {
    Users = [ me ]
    ActiveDriver =
      match String.IsNullOrWhiteSpace(data.ActiveDriver) with
      | true -> None
      | false ->
        Some {
          Name = data.ActiveDriver
          Creature = creatureByName data.ActiveDriver
        }
    CurrentUser = me
    Timer = Timer.init client sessionId
    Persistence = {
      Client = client
      SessionId = sessionId
    }
  }

let private activeDriverCmd (model: Model) (driver: string option) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Sessions.setActiveDriver model.Persistence.Client model.Persistence.SessionId driver)
    ()
    (fun () -> ActiveDriverSaved)

let private feedTimer (model: Model) : Timer.Model * Cmd<Msg> =
  let connectedUsers = model.Users |> List.map (fun u -> u.Name)
  let activeDriver = model.ActiveDriver |> Option.map (fun u -> u.Name)
  let userAvatarMap = model.Users |> List.map (fun u -> u.Name, u.Creature) |> Map.ofList

  let timerM, timerCmd = Timer.update (Timer.SessionUpdated(connectedUsers, activeDriver, userAvatarMap)) model.Timer

  timerM, Cmd.map TimerMsg timerCmd

let update msg model =
  match msg with
  | UpdateSession data ->
    let withDriver = {
      model with
          ActiveDriver =
            match String.IsNullOrWhiteSpace(data.ActiveDriver) with
            | true -> None
            | false -> model.Users |> List.tryFind (fun u -> u.Name = data.ActiveDriver)
    }

    let timerM, timerCmd = feedTimer withDriver
    { withDriver with Timer = timerM }, timerCmd

  | RemoteUserChanged(user, presence) -> upsertUser user presence model, []
  | RemoteUserRemoved user -> removeUser user model, []

  | SetActiveDriver driver -> model, activeDriverCmd model driver
  | ActiveDriverSaved -> model, []

  | SwitchDriver ->
    let users = model.Users

    let nextUser =
      match model.ActiveDriver with
      | None -> users |> List.tryHead
      | Some current ->
        let idx =
          users
          |> List.tryFindIndex (fun u -> u.Name = current.Name)
          |> Option.defaultValue -1

        Some users.[(idx + 1) % users.Length]

    let nextDriverName = nextUser |> Option.map (fun u -> u.Name)
    let connectedNames = users |> List.map (fun u -> u.Name)
    let avatarMap = users |> List.map (fun u -> u.Name, u.Creature) |> Map.ofList

    let m = {
      model with
          ActiveDriver = nextUser
          Timer = Timer.resetForDriver model.Timer nextDriverName connectedNames avatarMap
    }

    m, Cmd.batch [ activeDriverCmd m nextDriverName; Cmd.ofMsg (TimerMsg Timer.Start) ]

  | TimerMsg tMsg ->
    let m, cmd = Timer.update tMsg model.Timer
    { model with Timer = m }, Cmd.map TimerMsg cmd

let private subMap (wrap: 'a -> 'b) (subs: (string list * (Dispatch<'a> -> IDisposable)) list) =
  subs
  |> List.map (fun (key, start) -> key, (fun (dispatch: Dispatch<'b>) -> start (wrap >> dispatch)))

let subscriptions (model: Model) =
  (Firebase.Users.subscription model.Persistence.Client model.Persistence.SessionId "journey" (fun ev ->
    match ev with
    | Firebase.UserChanged(user, presence) -> RemoteUserChanged(user, presence)
    | Firebase.UserRemoved user -> RemoteUserRemoved user))
  @ (Timer.subscriptions model.Timer |> subMap TimerMsg)

let keyMap (_model: Model) : Spectre.Tui.App.IKeyMap =
  { new Spectre.Tui.App.IKeyMap with
      member _.Help() =
        Seq.empty
  }

let private avatarColor (creature: Creature) =
  creature.SmallRows
  |> List.concat
  |> List.tryPick (function
    | Filled c -> Some c
    | Empty -> None)
  |> Option.defaultValue Color.Silver

let private journeyLayout =
  layout "journey"
  |> splitVertically [|
    layout "pad-left" |> withFixedSize (Some 1)
    layout "users" |> withFixedSize (Some 20)
    layout "road"
    layout "pad-right" |> withFixedSize (Some 1)
  |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let port = getPort context.Viewport journeyLayout

        let userLine (user: User) =
          let color = avatarColor user.Creature
          let box = Text.styledSpan (Nullable(Style color)) "██"

          let isDriver =
            match model.ActiveDriver with
            | Some d -> d.Name = user.Name
            | None -> false

          let nameSpan =
            match isDriver with
            | true -> Text.styledSpan (Nullable(Style color)) (sprintf "▶ %s" user.Name)
            | false -> Text.span (sprintf "  %s" user.Name)

          Text.line [ box; Text.span " "; nameSpan ]

        let userLines = model.Users |> List.map userLine

        context.Render(paragraph userLines |> withOverflow Overflow.Ellipsis, port "users")

        context.Render(Timer.widget model.Timer, port "road")
  }
