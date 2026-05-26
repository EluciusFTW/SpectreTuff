module Avatar

open System
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open Keymap
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

let moodToString (mood: Mood) =
  match mood with
  | Happy -> "Happy"
  | Neutral -> "Neutral"
  | Sad -> "Sad"

let private moodFromString (s: string) =
  match s with
  | "Happy" -> Happy
  | "Sad" -> Sad
  | _ -> Neutral

type User = {
  Name: string
  Creature: Creature
  Mood: Mood
}

type Persistence = {
  Client: FirebaseClient
  SessionId: string
}

type Model = {
  Users: User list
  ActiveDriver: User option
  CurrentUser: User
  Persistence: Persistence
}

type Msg =
  | NextMood
  | UpdateSession of Session.Data
  | RemoteUserChanged of user: string * presence: Session.UserPresence
  | RemoteUserRemoved of user: string
  | SetActiveDriver of string option
  | PresenceSaved
  | ActiveDriverSaved

let private upsertUser (userName: string) (presence: Session.UserPresence) (model: Model) : Model =
  let avatarName: string =
    match isNull (presence :> obj) with
    | true -> null
    | false -> presence.Avatar

  let moodStr: string =
    match isNull (presence :> obj) with
    | true -> null
    | false -> presence.Mood

  let mood = moodFromString moodStr

  let updated =
    match userName = model.CurrentUser.Name with
    | true -> { model.CurrentUser with Mood = mood }
    | false -> {
        Name = userName
        Creature = creatureByName avatarName
        Mood = mood
      }

  let users =
    match model.Users |> List.exists (fun u -> u.Name = userName) with
    | true -> model.Users |> List.map (fun u -> if u.Name = userName then updated else u)
    | false -> model.Users @ [ updated ]

  let currentUser =
    match userName = model.CurrentUser.Name with
    | true -> updated
    | false -> model.CurrentUser

  {
    model with
        Users = users
        CurrentUser = currentUser
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
    Mood = Neutral
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
          Mood = Neutral
        }
    CurrentUser = me
    Persistence = {
      Client = client
      SessionId = sessionId
    }
  }

let private presenceCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () ->
      Firebase.Users.setPresence
        model.Persistence.Client
        model.Persistence.SessionId
        model.CurrentUser.Name
        model.CurrentUser.Creature.Name
        (moodToString model.CurrentUser.Mood))
    ()
    (fun () -> PresenceSaved)

let private activeDriverCmd (model: Model) (driver: string option) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Sessions.setActiveDriver model.Persistence.Client model.Persistence.SessionId driver)
    ()
    (fun () -> ActiveDriverSaved)

let update msg model =
  match msg with
  | NextMood ->
    let next =
      match model.CurrentUser.Mood with
      | Happy -> Neutral
      | Neutral -> Sad
      | Sad -> Happy

    let updated = { model.CurrentUser with Mood = next }

    let m = {
      model with
          CurrentUser = updated
          Users =
            model.Users
            |> List.map (fun u ->
              match u.Name = updated.Name with
              | true -> updated
              | false -> u)
          ActiveDriver =
            match model.ActiveDriver with
            | Some d when d.Name = updated.Name -> Some updated
            | other -> other
    }

    m, presenceCmd m

  | UpdateSession data ->
    {
      model with
          ActiveDriver =
            match String.IsNullOrWhiteSpace(data.ActiveDriver) with
            | true -> None
            | false -> model.Users |> List.tryFind (fun u -> u.Name = data.ActiveDriver)
    },
    []

  | RemoteUserChanged(user, presence) -> upsertUser user presence model, []
  | RemoteUserRemoved user -> removeUser user model, []
  | SetActiveDriver driver -> model, activeDriverCmd model driver
  | PresenceSaved -> model, []
  | ActiveDriverSaved -> model, []

let subscriptions (model: Model) =
  Firebase.Users.subscription model.Persistence.Client model.Persistence.SessionId (fun ev ->
    match ev with
    | Firebase.UserChanged(user, presence) -> RemoteUserChanged(user, presence)
    | Firebase.UserRemoved user -> RemoteUserRemoved user)

let private bindings: KeyBinding<Model, Msg> list = [ KeyBinding.create 'm' "mood" NextMood ]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let private withDriverLayout =
  layout "av-with-driver"
  |> splitHorizontally [| layout "driver-box" |> withFixedSize (Some 15); layout "others-area" |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let renderCell cell =
          match cell with
          | Empty -> Text.span "  "
          | Filled color -> Text.styledSpan (System.Nullable(Style color)) "██"

        let bigLinesWithName (user: User) =
          let nameLine = Text.line [ Text.span user.Name ]

          let avatarLines =
            user.Creature.Rows user.Mood
            |> List.map (fun row -> row |> List.map renderCell |> Text.line)

          nameLine :: avatarLines

        match model.ActiveDriver with
        | None ->
          let lines = model.Users |> List.collect bigLinesWithName
          context.Render(paragraph lines |> withHorizontalAlignment Justify.Center, context.Viewport)

        | Some driverUser ->
          let port = getPort context.Viewport withDriverLayout

          let driverContent =
            { new IWidget with
                member _.Render(ctx: RenderContext) =
                  ctx.Render(
                    paragraph (bigLinesWithName driverUser)
                    |> withHorizontalAlignment Justify.Center,
                    ctx.Viewport
                  )
            }

          context.Render(box Look.empty |> withTitle "Driver" |> withInnerWidget driverContent, port "driver-box")

          let others = model.Users |> List.filter (fun u -> u.Name <> driverUser.Name)

          context.Render(
            paragraph (others |> List.collect bigLinesWithName)
            |> withHorizontalAlignment Justify.Center,
            port "others-area"
          )
  }
