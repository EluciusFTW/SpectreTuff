module Avatar

open System
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

let private creatureByName (name: string) =
  if String.IsNullOrWhiteSpace(name) then
    library.[Random.Shared.Next(library.Length)]
  else
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

type Model = {
  Users: User list
  ActiveDriver: User option
  CurrentUser: User
}

type Msg =
  | NextMood
  | BecomeDriver
  | UpdateSession of Session.Data

let private buildUsers (data: Session.Data) (currentUser: string) (myCreature: Creature) (myMood: Mood) =
  if isNull data.ConnectedUsers then
    [
      {
        Name = currentUser
        Creature = myCreature
        Mood = myMood
      }
    ]
  else
    data.ConnectedUsers
    |> Seq.map (fun kv ->
      match kv.Key = currentUser with
      | true -> {
          Name = kv.Key
          Creature = myCreature
          Mood = myMood
        }
      | false -> {
          Name = kv.Key
          Creature = creatureByName kv.Value.Avatar
          Mood = moodFromString kv.Value.Mood
        })
    |> Seq.toList

let init (currentUser: string) (data: Session.Data) =
  let myCreature = resolveCreature ()
  let myMood = Neutral
  let users = buildUsers data currentUser myCreature myMood

  let me =
    users
    |> List.tryFind (fun u -> u.Name = currentUser)
    |> Option.defaultValue {
      Name = currentUser
      Creature = myCreature
      Mood = myMood
    }

  {
    Users = users
    ActiveDriver =
      match String.IsNullOrWhiteSpace(data.ActiveDriver) with
      | true -> None
      | false -> users |> List.tryFind (fun u -> u.Name = data.ActiveDriver)
    CurrentUser = me
  }

let update msg model =
  match msg with
  | NextMood ->
    let next =
      match model.CurrentUser.Mood with
      | Happy -> Neutral
      | Neutral -> Sad
      | Sad -> Happy

    let updated = { model.CurrentUser with Mood = next }

    {
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
    },
    []

  | BecomeDriver -> model, []

  | UpdateSession data ->
    let users = buildUsers data model.CurrentUser.Name model.CurrentUser.Creature model.CurrentUser.Mood

    let me =
      users
      |> List.tryFind (fun u -> u.Name = model.CurrentUser.Name)
      |> Option.defaultValue model.CurrentUser

    {
      Users = users
      CurrentUser = me
      ActiveDriver =
        match String.IsNullOrWhiteSpace(data.ActiveDriver) with
        | true -> None
        | false -> users |> List.tryFind (fun u -> u.Name = data.ActiveDriver)
    },
    []

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.create 'm' "mood" NextMood
  KeyBinding.create 'd' "drive" BecomeDriver
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let private driverLayout =
  layout "av-root"
  |> splitHorizontally [| layout "driver-area" |> withRatio 3; layout "others-area" |> withRatio 2 |]

let private driverNameLayout =
  layout "driver-with-name"
  |> splitHorizontally [| layout "big-av"; layout "driver-lbl" |> withFixedSize (Some 1) |]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let renderCell cell =
          match cell with
          | Empty -> Text.span "  "
          | Filled color -> Text.styledSpan (System.Nullable(Style color)) "██"

        let renderSmall (user: User) =
          let avatarLines =
            user.Creature.SmallRows
            |> List.map (fun row -> row |> List.map renderCell |> Text.line)

          let nameLine = Text.line [ Text.span user.Name ]
          avatarLines @ [ nameLine ]

        match model.ActiveDriver with
        | None ->
          let lines = model.Users |> List.collect renderSmall
          context.Render(paragraph lines |> withHorizontalAlignment Justify.Center, context.Viewport)

        | Some driverUser ->
          let port = getPort context.Viewport driverLayout
          let driverPort = getPort (port "driver-area") driverNameLayout

          context.Render(avatar driverUser.Mood driverUser.Creature :> IWidget, driverPort "big-av")

          context.Render(
            paragraph [ Text.line [ Text.span driverUser.Name ] ]
            |> withHorizontalAlignment Justify.Center,
            driverPort "driver-lbl"
          )

          let others = model.Users |> List.filter (fun u -> u.Name <> driverUser.Name)
          let otherLines = others |> List.collect renderSmall

          context.Render(paragraph otherLines |> withHorizontalAlignment Justify.Center, port "others-area")
  }
