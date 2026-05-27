namespace SpectreTuff.Widgets

open Spectre.Console
open Spectre.Tui
open SpectreTuff

type Cell =
  | Empty
  | Filled of Color

type Mood =
  | Happy
  | Neutral
  | Sad

type Creature = {
  Name: string
  Rows: Mood -> Cell list list
  SmallRows: Cell list list
}

[<AutoOpen>]
module Avatar =

  let private e = Empty
  let private f = Filled

  let private G = f Color.Green
  let private Y = f Color.Yellow
  let private K = f Color.Black
  let private W = f Color.White
  let private B = f Color.Blue
  let private R = f Color.Red
  let private r = f Color.Maroon
  let private P = f Color.Purple
  let private p = f Color.Fuchsia
  let private S = f Color.Silver
  let private O = f Color.Orange1
  let private C = f Color.Aqua
  let private Tc = f Color.SkyBlue1
  let private Pk = f Color.HotPink

  let private blobboRows mood =
    let top = [
      [ e; e; e; G; G; G; G; G; e; e; e; e ]
      [ e; e; G; G; G; G; G; G; G; G; e; e ]
      [ e; G; G; G; G; G; G; G; G; G; G; e ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; Y; K; Y; G; G; Y; K; Y; G; G ]
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; e; W; W; W; W; W; W; W; W; e; G ]
          [ e; e; G; W; W; W; W; W; W; G; e; e ]
        ]
      | Neutral -> [
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; Y; K; G; G; G; G; Y; K; G; G ]
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; G; e; G; G; G; G; e; G; G; G ]
        ]
      | Sad -> [
          [ G; G; G; G; G; G; G; G; G; G; G; G ]
          [ G; G; K; K; K; G; G; K; K; K; G; G ]
          [ G; Tc; Tc; Tc; Tc; G; G; Tc; Tc; Tc; Tc; G ]
          [ Tc; Tc; Tc; Tc; G; G; G; G; Tc; Tc; Tc; Tc ]
          [ G; e; e; e; e; e; e; e; e; e; e; G ]
        ]

    let bot = [
      [ e; G; G; G; G; G; G; G; G; G; G; e ]
      [ e; e; G; G; G; G; G; G; G; G; e; e ]
      [ e; e; e; G; G; G; G; G; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private gloopRows mood =
    let top = [
      [ e; e; P; P; P; e; P; P; P; e; e; e ]
      [ e; P; P; P; P; P; P; P; P; P; e; e ]
      [ P; P; P; P; P; P; P; P; P; P; P; e ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; C; K; C; P; P; C; K; C; P; P ]
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; e; W; W; W; W; W; W; W; W; e; P ]
          [ e; e; P; W; W; W; W; W; W; P; e; e ]
        ]
      | Neutral -> [
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; C; K; P; P; P; P; C; K; P; P ]
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; P; e; P; P; P; P; e; P; P; P ]
        ]
      | Sad -> [
          [ P; P; P; P; P; P; P; P; P; P; P; P ]
          [ P; P; K; K; K; P; P; K; K; K; P; P ]
          [ P; Tc; Tc; Tc; Tc; P; P; Tc; Tc; Tc; Tc; P ]
          [ Tc; Tc; Tc; Tc; P; P; P; P; Tc; Tc; Tc; Tc ]
          [ P; e; e; e; e; e; e; e; e; e; e; P ]
        ]

    let bot = [
      [ P; P; P; P; P; P; P; P; P; P; P; e ]
      [ e; P; P; P; P; P; P; P; P; P; e; e ]
      [ e; e; P; P; P; P; P; P; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private zorpRows mood =
    let top = [
      [ e; e; B; B; B; B; B; B; B; B; e; e ]
      [ e; B; B; B; B; B; B; B; B; B; B; e ]
      [ B; B; B; B; B; B; B; B; B; B; B; B ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; G; K; G; B; B; G; K; G; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; e; W; W; W; W; W; W; W; W; e; B ]
          [ e; e; B; W; W; W; W; W; W; B; e; e ]
        ]
      | Neutral -> [
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; G; K; B; B; B; B; G; K; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; B; e; B; B; B; B; e; B; B; B ]
        ]
      | Sad -> [
          [ B; B; B; B; B; B; B; B; B; B; B; B ]
          [ B; B; K; K; K; B; B; K; K; K; B; B ]
          [ B; Tc; Tc; Tc; Tc; B; B; Tc; Tc; Tc; Tc; B ]
          [ Tc; Tc; Tc; Tc; B; B; B; B; Tc; Tc; Tc; Tc ]
          [ B; e; e; e; e; e; e; e; e; e; e; B ]
        ]

    let bot = [
      [ B; B; B; B; B; B; B; B; B; B; B; B ]
      [ e; B; B; B; B; B; B; B; B; B; B; e ]
      [ e; e; B; B; B; B; B; B; B; B; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private blipRows mood =
    let top = [
      [ e; e; C; C; C; C; C; C; e; e; e; e ]
      [ e; C; C; C; C; C; C; C; C; C; e; e ]
      [ C; C; C; C; C; C; C; C; C; C; C; e ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; B; K; B; C; C; B; K; B; C; C ]
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; e; W; W; W; W; W; W; W; W; e; C ]
          [ e; e; C; W; W; W; W; W; W; C; e; e ]
        ]
      | Neutral -> [
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; B; K; C; C; C; B; K; C; C; C ]
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; C; e; C; C; C; C; e; C; C; C ]
        ]
      | Sad -> [
          [ C; C; C; C; C; C; C; C; C; C; C; C ]
          [ C; C; K; K; K; C; C; K; K; K; C; C ]
          [ C; Tc; Tc; Tc; Tc; C; C; Tc; Tc; Tc; Tc; C ]
          [ Tc; Tc; Tc; Tc; C; C; C; C; Tc; Tc; Tc; Tc ]
          [ C; e; e; e; e; e; e; e; e; e; e; C ]
        ]

    let bot = [
      [ e; C; C; C; C; C; C; C; C; C; C; e ]
      [ e; e; C; C; C; C; C; C; C; C; e; e ]
      [ e; e; e; C; C; C; C; C; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private grompRows mood =
    let top = [
      [ e; e; S; S; S; S; S; S; S; S; e; e ]
      [ e; S; S; S; S; S; S; S; S; S; S; e ]
      [ S; S; S; K; S; S; S; S; K; S; S; S ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; W; K; W; S; S; W; K; W; S; S ]
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; e; W; W; W; W; W; W; W; W; e; S ]
          [ e; e; S; W; W; W; W; W; W; S; e; e ]
        ]
      | Neutral -> [
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; W; K; S; S; S; S; W; K; S; S ]
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; S; e; S; S; S; S; e; S; S; S ]
        ]
      | Sad -> [
          [ S; S; S; S; S; S; S; S; S; S; S; S ]
          [ S; S; K; K; K; S; S; K; K; K; S; S ]
          [ S; Tc; Tc; Tc; Tc; S; S; Tc; Tc; Tc; Tc; S ]
          [ Tc; Tc; Tc; Tc; S; S; S; S; Tc; Tc; Tc; Tc ]
          [ S; e; e; e; e; e; e; e; e; e; e; S ]
        ]

    let bot = [
      [ e; S; S; S; S; S; S; S; S; S; S; e ]
      [ e; e; S; S; S; S; S; S; S; S; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private squigRows mood =
    let top = [
      [ e; e; e; Y; Y; Y; Y; Y; Y; e; e; e ]
      [ e; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; e ]
      [ Y; Y; Y; Y; R; Y; Y; R; Y; Y; Y; Y ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ Y; Y; K; e; K; Y; Y; K; e; K; Y; Y ]
          [ Y; Pk; Pk; Y; Y; Y; Y; Y; Y; Pk; Pk; Y ]
          [ Y; e; W; W; W; W; W; W; W; W; e; Y ]
          [ e; e; Y; W; W; W; W; W; W; Y; e; e ]
        ]
      | Neutral -> [
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ Y; Y; K; e; Y; Y; Y; Y; K; e; Y; Y ]
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ Y; Y; Y; R; Y; Y; Y; Y; R; Y; Y; Y ]
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
        ]
      | Sad -> [
          [ Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y ]
          [ Y; Y; K; K; K; Y; Y; K; K; K; Y; Y ]
          [ Y; Tc; Tc; Tc; Tc; Y; Y; Tc; Tc; Tc; Tc; Y ]
          [ Tc; Tc; Tc; Tc; Y; Y; Y; Y; Tc; Tc; Tc; Tc ]
          [ Y; e; e; e; e; e; e; e; e; e; e; Y ]
        ]

    let bot = [
      [ e; Y; Y; Y; Y; Y; Y; Y; Y; Y; Y; e ]
      [ e; e; Y; Y; Y; R; Y; Y; Y; Y; e; e ]
      [ e; e; e; Y; Y; Y; Y; Y; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private spikoRows mood =
    let top = [
      [ e; B; e; e; e; e; e; e; e; B; e; e ]
      [ B; B; e; e; e; e; e; e; e; B; B; e ]
      [ e; B; e; e; R; R; R; R; e; B; e; e ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ e; e; e; R; R; R; R; R; R; e; e; e ]
          [ e; e; R; R; Y; Y; R; Y; Y; R; R; e ]
          [ e; e; R; Pk; R; R; R; R; Pk; R; R; e ]
          [ e; e; R; e; W; W; W; W; W; e; R; e ]
          [ e; e; R; R; e; W; W; W; e; R; R; e ]
        ]
      | Neutral -> [
          [ e; e; e; R; R; R; R; R; R; e; e; e ]
          [ e; e; R; R; Y; K; R; Y; K; R; R; e ]
          [ e; e; R; R; R; R; R; R; R; R; R; e ]
          [ e; e; R; R; e; R; R; e; R; R; R; e ]
          [ e; e; R; R; R; e; e; R; R; R; e; e ]
        ]
      | Sad -> [
          [ e; e; e; R; R; R; R; R; R; e; e; e ]
          [ e; e; R; R; K; K; R; K; K; R; R; e ]
          [ e; e; R; R; Tc; Tc; R; Tc; Tc; R; R; e ]
          [ e; e; Tc; Tc; Tc; R; R; R; Tc; Tc; Tc; e ]
          [ e; e; R; R; e; e; e; e; R; R; e; e ]
        ]

    let bot = [
      [ e; e; e; R; R; R; R; R; R; R; e; e ]
      [ e; e; e; e; R; R; R; R; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private fangsRows mood =
    let top = [
      [ e; e; r; r; r; r; r; r; r; e; e; e ]
      [ e; r; r; r; r; r; r; r; r; r; r; e ]
      [ r; r; r; r; r; r; r; r; r; r; r; r ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ r; r; O; O; r; r; r; r; O; O; r; r ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; Pk; r; r; r; r; r; r; r; r; Pk; r ]
          [ e; r; W; W; W; W; W; W; W; W; r; e ]
          [ e; r; W; e; W; W; W; W; e; W; r; e ]
        ]
      | Neutral -> [
          [ r; r; O; e; r; r; r; r; O; e; r; r ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; r; e; W; e; r; r; e; W; e; r; r ]
          [ r; r; W; W; W; r; r; W; W; W; r; r ]
        ]
      | Sad -> [
          [ r; r; e; O; r; r; r; r; e; O; r; r ]
          [ r; r; r; r; r; r; r; r; r; r; r; r ]
          [ r; r; Tc; r; r; r; r; r; Tc; r; r; r ]
          [ r; Tc; Tc; r; r; r; r; r; r; Tc; Tc; r ]
          [ r; r; r; e; e; e; e; e; e; r; r; r ]
        ]

    let bot = [
      [ e; r; r; r; r; r; r; r; r; r; r; e ]
      [ e; e; r; r; r; r; r; r; r; r; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private twirlRows mood =
    let top = [
      [ e; e; e; e; p; p; p; e; e; e; e; e ]
      [ e; e; e; p; p; p; p; p; e; e; e; e ]
      [ e; e; p; p; p; p; p; p; p; e; e; e ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ e; p; p; p; p; p; p; p; p; p; e; e ]
          [ p; p; W; W; e; W; W; e; W; p; p; e ]
          [ p; Pk; p; p; p; p; p; p; p; p; Pk; e ]
          [ p; e; W; W; W; W; W; W; W; W; e; p ]
          [ e; e; p; W; W; W; W; W; W; p; e; e ]
        ]
      | Neutral -> [
          [ e; p; p; p; p; p; p; p; p; p; e; e ]
          [ p; p; p; p; W; e; W; e; p; p; p; e ]
          [ p; p; p; p; p; p; p; p; p; p; p; e ]
          [ p; p; W; p; p; p; p; p; p; W; p; p ]
          [ e; p; p; p; p; W; W; p; p; p; p; e ]
        ]
      | Sad -> [
          [ e; p; p; p; p; p; p; p; p; p; e; e ]
          [ p; p; p; p; K; e; K; e; p; p; p; e ]
          [ p; p; Tc; p; p; p; p; p; Tc; p; p; e ]
          [ p; Tc; Tc; p; p; p; p; p; Tc; Tc; p; e ]
          [ e; p; p; e; e; e; e; e; e; p; p; e ]
        ]

    let bot = [
      [ e; e; p; p; p; p; p; p; p; p; e; e ]
      [ e; e; e; p; p; p; p; p; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let private krexRows mood =
    let top = [
      [ e; e; O; O; e; e; e; O; O; e; e; e ]
      [ e; e; e; O; O; O; O; O; e; e; e; e ]
      [ e; e; O; O; O; O; O; O; O; O; e; e ]
    ]

    let face =
      match mood with
      | Happy -> [
          [ e; O; O; O; O; O; O; O; O; O; O; e ]
          [ e; O; W; K; W; O; O; W; K; W; O; e ]
          [ O; O; O; O; O; O; O; O; O; O; O; O ]
          [ O; e; W; W; W; W; W; W; W; W; e; O ]
          [ e; e; O; W; W; W; W; W; W; O; e; e ]
        ]
      | Neutral -> [
          [ e; O; O; O; O; O; O; O; O; O; O; e ]
          [ e; O; O; W; K; O; O; W; K; O; O; e ]
          [ O; O; O; O; O; O; O; O; O; O; O; O ]
          [ O; O; r; O; O; O; O; O; O; r; O; O ]
          [ O; O; O; O; O; r; r; O; O; O; O; O ]
        ]
      | Sad -> [
          [ e; O; O; O; O; O; O; O; O; O; O; e ]
          [ e; O; O; K; K; O; O; K; K; O; O; e ]
          [ e; O; Tc; Tc; Tc; O; O; Tc; Tc; Tc; O; e ]
          [ O; Tc; Tc; Tc; O; O; O; O; Tc; Tc; Tc; O ]
          [ O; O; O; e; e; e; e; e; e; O; O; O ]
        ]

    let bot = [
      [ e; O; O; O; O; O; O; O; O; O; O; e ]
      [ e; e; O; O; O; O; O; O; O; O; e; e ]
      [ e; e; e; O; O; O; O; O; e; e; e; e ]
      [ e; e; e; e; e; e; e; e; e; e; e; e ]
    ]

    top @ face @ bot

  let library: Creature list = [
    {
      Name = "Blobbo"
      Rows = blobboRows
      SmallRows = [
        [ e; G; G; G; G; e ]
        [ G; G; G; G; G; G ]
        [ G; Y; G; G; Y; G ]
        [ G; G; G; G; G; G ]
        [ e; G; G; G; G; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Spiko"
      Rows = spikoRows
      SmallRows = [
        [ e; B; e; e; B; e ]
        [ R; R; R; R; R; R ]
        [ R; Y; R; R; Y; R ]
        [ R; R; R; R; R; R ]
        [ e; R; R; R; R; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Gloop"
      Rows = gloopRows
      SmallRows = [
        [ e; P; P; P; e; e ]
        [ P; P; P; P; P; e ]
        [ P; C; P; P; C; P ]
        [ P; P; P; P; P; P ]
        [ e; P; P; P; P; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Fangs"
      Rows = fangsRows
      SmallRows = [
        [ e; r; r; r; r; e ]
        [ r; r; r; r; r; r ]
        [ r; O; e; r; O; e ]
        [ r; r; r; r; r; r ]
        [ r; W; r; r; W; r ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Zorp"
      Rows = zorpRows
      SmallRows = [
        [ e; B; B; B; B; e ]
        [ B; B; B; B; B; B ]
        [ B; G; B; B; G; B ]
        [ B; B; B; B; B; B ]
        [ e; B; B; B; B; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Squig"
      Rows = squigRows
      SmallRows = [
        [ e; Y; Y; Y; Y; e ]
        [ Y; Y; Y; Y; Y; Y ]
        [ Y; K; e; Y; K; e ]
        [ Y; Y; Y; Y; Y; Y ]
        [ e; Y; Y; Y; Y; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Gromp"
      Rows = grompRows
      SmallRows = [
        [ e; S; S; S; S; e ]
        [ S; S; S; S; S; S ]
        [ S; W; S; S; W; S ]
        [ S; S; S; S; S; S ]
        [ e; S; S; S; S; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Twirl"
      Rows = twirlRows
      SmallRows = [
        [ e; e; p; p; e; e ]
        [ e; p; p; p; p; e ]
        [ p; W; e; W; e; p ]
        [ p; p; p; p; p; e ]
        [ e; p; p; p; e; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Blip"
      Rows = blipRows
      SmallRows = [
        [ e; C; C; C; e; e ]
        [ C; C; C; C; C; e ]
        [ C; B; C; C; B; C ]
        [ C; C; C; C; C; C ]
        [ e; C; C; C; C; e ]
        [ e; e; e; e; e; e ]
      ]
    }
    {
      Name = "Krex"
      Rows = krexRows
      SmallRows = [
        [ O; e; O; O; e; O ]
        [ O; O; O; O; O; O ]
        [ O; W; O; O; W; O ]
        [ O; O; r; r; O; O ]
        [ e; O; O; O; O; e ]
        [ e; e; e; e; e; e ]
      ]
    }
  ]

  let private renderCell cell =
    match cell with
    | Empty -> Text.span "  "
    | Filled color -> Text.styledSpan (System.Nullable(Style color)) "██"

  let private normalizeIndex index =
    ((index % library.Length) + library.Length) % library.Length

  type AvatarWidget(creature: Creature, mood: Mood) =
    interface IWidget with
      member _.Render(context: RenderContext) =
        let lines =
          creature.Rows mood
          |> List.map (fun row -> row |> List.map renderCell |> Text.line)

        context.Render(paragraph lines, context.Viewport)

  let avatar (mood: Mood) (creature: Creature) =
    AvatarWidget(creature, mood)

  let randomAvatar (mood: Mood) =
    let idx = System.Random.Shared.Next(library.Length)
    AvatarWidget(library[idx], mood)

  let avatarByIndex (mood: Mood) (index: int) =
    AvatarWidget(library[normalizeIndex index], mood)

  type SmallAvatarWidget(creature: Creature) =
    interface IWidget with
      member _.Render(context: RenderContext) =
        let lines =
          creature.SmallRows
          |> List.map (fun row -> row |> List.map renderCell |> Text.line)

        context.Render(paragraph lines, context.Viewport)

  let smallAvatar (creature: Creature) =
    SmallAvatarWidget(creature)

  let smallAvatarByIndex (index: int) =
    SmallAvatarWidget(library[normalizeIndex index])
