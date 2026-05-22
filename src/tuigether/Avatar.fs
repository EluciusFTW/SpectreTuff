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

type Model = {
  ConnectedUsers: string list
  UserAvatars: Map<string, Creature>
  ActiveDriver: string option
  CurrentUser: string
  MyCreature: Creature
  Mood: Mood
}

type Msg =
  | NextMood
  | BecomeDriver
  | UpdateSession of Session.Data

let private buildUserAvatars (data: Session.Data) (currentUser: string) (myCreature: Creature) =
  let base' =
    if isNull data.ConnectedUsers then
      Map.empty
    else
      data.ConnectedUsers
      |> Seq.map (fun kv -> kv.Key, creatureByName kv.Value)
      |> Map.ofSeq

  base' |> Map.add currentUser myCreature

let init (currentUser: string) (data: Session.Data) =
  let myCreature = resolveCreature ()

  {
    ConnectedUsers =
      if isNull data.ConnectedUsers then
        []
      else
        data.ConnectedUsers.Keys |> Seq.toList
    UserAvatars = buildUserAvatars data currentUser myCreature
    ActiveDriver =
      if String.IsNullOrWhiteSpace(data.ActiveDriver) then
        None
      else
        Some data.ActiveDriver
    CurrentUser = currentUser
    MyCreature = myCreature
    Mood = Neutral
  }

let update msg model =
  match msg with
  | NextMood ->
    match model.ActiveDriver with
    | Some driver when driver = model.CurrentUser ->
      let next =
        match model.Mood with
        | Happy -> Neutral
        | Neutral -> Sad
        | Sad -> Happy

      { model with Mood = next }, []
    | _ -> model, []

  | BecomeDriver -> model, []

  | UpdateSession data ->
    let userAvatars = buildUserAvatars data model.CurrentUser model.MyCreature

    {
      model with
          ConnectedUsers =
            if isNull data.ConnectedUsers then
              []
            else
              data.ConnectedUsers.Keys |> Seq.toList
          UserAvatars = userAvatars
          ActiveDriver =
            if String.IsNullOrWhiteSpace(data.ActiveDriver) then
              None
            else
              Some data.ActiveDriver
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

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let getCreature user =
          model.UserAvatars |> Map.tryFind user |> Option.defaultValue library.[0]

        let renderCell cell =
          match cell with
          | Empty -> Text.span "  "
          | Filled color -> Text.styledSpan (System.Nullable(Style color)) "██"

        match model.ActiveDriver with
        | None ->
          let lines =
            model.ConnectedUsers
            |> List.collect (fun user ->
              let label =
                if user = model.CurrentUser then
                  $"▶ {user}"
                else
                  $"  {user}"

              let creature = getCreature user
              let labelLine = Text.line [ Text.span label ]

              let avatarLines =
                creature.SmallRows
                |> List.map (fun row -> row |> List.map renderCell |> Text.line)

              labelLine :: avatarLines)

          context.Render(paragraph lines, context.Viewport)

        | Some driverUser ->
          let port = getPort context.Viewport driverLayout

          let driverCreature = getCreature driverUser

          let driverMood =
            if driverUser = model.CurrentUser then
              model.Mood
            else
              Neutral

          context.Render(avatar driverMood driverCreature :> IWidget, port "driver-area")

          let others = model.ConnectedUsers |> List.filter (fun u -> u <> driverUser)

          let lines =
            others
            |> List.collect (fun user ->
              let label =
                if user = model.CurrentUser then
                  $"▶ {user}"
                else
                  $"  {user}"

              let creature = getCreature user
              let labelLine = Text.line [ Text.span label ]

              let avatarLines =
                creature.SmallRows
                |> List.map (fun row -> row |> List.map renderCell |> Text.line)

              labelLine :: avatarLines)

          context.Render(paragraph lines, port "others-area")
  }
