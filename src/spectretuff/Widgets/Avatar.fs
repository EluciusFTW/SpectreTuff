namespace SpectreTuff.Widgets

open Spectre.Console
open Spectre.Tui
open SpectreTuff

type Cell =
  | Empty
  | Filled of Color

type Creature = { Name: string; Rows: Cell list list }

[<AutoOpen>]
module Avatar =

  let private e = Empty
  let private f = Filled

  let library: Creature list =
    let G = f Color.Green
    let Y = f Color.Yellow
    let K = f Color.Black
    let W = f Color.White
    let B = f Color.Blue
    let R = f Color.Red
    let r = f Color.Maroon
    let P = f Color.Purple
    let p = f Color.Fuchsia
    let S = f Color.Silver
    let O = f Color.Orange1
    let C = f Color.Aqua

    [
      {
        Name = "Blobbo"
        Rows = [
          [ e; e; e; G; G; G; G; G; e; e; e; e ]
          [ e; e; G; G; G; G; G; G; G; G; e; e ]
          [ e; G; G; G; G; G; G; G; G; G; G; e ]
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; Y; K; G; G; G; G; Y; K; G; G ]
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; G; e; G; G; G; G; e; G; G; G ]
          [ e; G; G; G; G; G; G; G; G; G; G; e ]
          [ e; e; G; G; G; G; G; G; G; G; e; e ]
          [ e; e; e; G; G; G; G; G; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Spiko"
        Rows = [
          [ e; B; e; e; e; e; e; e; e; B; e; e ]
          [ B; B; e; e; e; e; e; e; e; B; B; e ]
          [ e; B; e; e; R; R; R; R; e; B; e; e ]
          [ e; e; e; R; R; R; R; R; R; e; e; e ]
          [ e; e; R; R; Y; K; R; Y; K; R; R; e ]
          [ e; e; R; R; R; R; R; R; R; R; R; e ]
          [ e; e; R; R; e; R; R; e; R; R; R; e ]
          [ e; e; R; R; R; e; e; R; R; R; e; e ]
          [ e; e; e; R; R; R; R; R; R; R; e; e ]
          [ e; e; e; e; R; R; R; R; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Gloop"
        Rows = [
          [ e; e; P; P; P; e; P; P; P; e; e; e ]
          [ e; P; P; P; P; P; P; P; P; P; e; e ]
          [ P; P; P; P; P; P; P; P; P; P; P; e ]
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; C; K; P; P; P; P; C; K; P; P ]
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; P; e; P; P; P; P; e; P; P; P ]
          [ P; P; P; P; P; P; P; P; P; P; P; e ]
          [ e; P; P; P; P; P; P; P; P; P; e; e ]
          [ e; e; P; P; P; P; P; P; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Fangs"
        Rows = [
          [ e; e; r; r; r; r; r; r; r; e; e; e ]
          [ e; r; r; r; r; r; r; r; r; r; r; e ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; r; O; e; r; r; r; r; O; e; r; r ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; r; e; W; e; r; r; e; W; e; r; r ]
          [ r; r; W; W; W; r; r; W; W; W; r; r ]
          [ e; r; r; r; r; r; r; r; r; r; r; e ]
          [ e; e; r; r; r; r; r; r; r; r; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Zorp"
        Rows = [
          [ e; e; B; B; B; B; B; B; B; B; e; e ]
          [ e; B; B; B; B; B; B; B; B; B; B; e ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; G; K; B; B; B; B; G; K; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ e; B; B; B; B; B; B; B; B; B; B; e ]
          [ e; e; B; B; B; B; B; B; B; B; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Squig"
        Rows = [
          [ e; e; e; Y; Y; Y; Y; Y; Y; e; e; e ]
          [ e; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; e ]
          [ Y; Y; Y; Y; R; Y; Y; R; Y; Y; Y; Y ]
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ Y; Y; K; e; Y; Y; Y; Y; K; e; Y; Y ]
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ Y; Y; Y; R; Y; Y; Y; Y; R; Y; Y; Y ]
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ e; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; e ]
          [ e; e; Y; Y; Y; R; Y; Y; Y; Y; e; e ]
          [ e; e; e; Y; Y; Y; Y; Y; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Gromp"
        Rows = [
          [ e; e; S; S; S; S; S; S; S; S; e; e ]
          [ e; S; S; S; S; S; S; S; S; S; S; e ]
          [ S; S; S; K; S; S; S; S; K; S; S; S ]
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; W; K; S; S; S; S; W; K; S; S ]
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; S; e; S; S; S; S; e; S; S; S ]
          [ e; S; S; S; S; S; S; S; S; S; S; e ]
          [ e; e; S; S; S; S; S; S; S; S; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Twirl"
        Rows = [
          [ e; e; e; e; p; p; p; e; e; e; e; e ]
          [ e; e; e; p; p; p; p; p; e; e; e; e ]
          [ e; e; p; p; p; p; p; p; p; e; e; e ]
          [ e; p; p; p; p; p; p; p; p; p; e; e ]
          [ p; p; p; p; W; e; W; e; p; p; p; e ]
          [ p; p; p; p; p; p; p; p; p; p; p; e ]
          [ p; p; W; p; p; p; p; p; p; W; p; p ]
          [ e; p; p; p; p; W; W; p; p; p; p; e ]
          [ e; e; p; p; p; p; p; p; p; p; e; e ]
          [ e; e; e; p; p; p; p; p; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Blip"
        Rows = [
          [ e; e; C; C; C; C; C; C; e; e; e; e ]
          [ e; C; C; C; C; C; C; C; C; C; e; e ]
          [ C; C; C; C; C; C; C; C; C; C; C; e ]
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; B; K; C; C; C; B; K; C; C; C ]
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; C; e; C; C; C; C; e; C; C; C ]
          [ e; C; C; C; C; C; C; C; C; C; C; e ]
          [ e; e; C; C; C; C; C; C; C; C; e; e ]
          [ e; e; e; C; C; C; C; C; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }

      {
        Name = "Krex"
        Rows = [
          [ e; e; O; O; e; e; e; O; O; e; e; e ]
          [ e; e; e; O; O; O; O; O; e; e; e; e ]
          [ e; e; O; O; O; O; O; O; O; O; e; e ]
          [ e; O; O; O; O; O; O; O; O; O; O; e ]
          [ e; O; O; W; K; O; O; W; K; O; O; e ]
          [ O; O; O; O; O; O; O; O; O; O; O; O ]
          [ O; O; r; O; O; O; O; O; O; r; O; O ]
          [ O; O; O; O; O; r; r; O; O; O; O; O ]
          [ e; O; O; O; O; O; O; O; O; O; O; e ]
          [ e; e; O; O; O; O; O; O; O; O; e; e ]
          [ e; e; e; O; O; O; O; O; e; e; e; e ]
          [ e; e; e; e; e; e; e; e; e; e; e; e ]
        ]
      }
    ]

  type AvatarWidget(creature: Creature) =
    interface IWidget with
      member _.Render(context: RenderContext) =
        let renderCell cell =
          match cell with
          | Empty -> Text.span "  "
          | Filled color -> Text.styledSpan (System.Nullable(Style color)) "██"

        let lines = creature.Rows |> List.map (fun row -> row |> List.map renderCell |> Text.line)

        context.Render(paragraph lines, context.Viewport)

  let avatar (creature: Creature) =
    AvatarWidget(creature)

  let randomAvatar () =
    let idx = System.Random.Shared.Next(library.Length)
    AvatarWidget(library[idx])

  let avatarByIndex (index: int) =
    let idx = ((index % library.Length) + library.Length) % library.Length
    AvatarWidget(library[idx])
