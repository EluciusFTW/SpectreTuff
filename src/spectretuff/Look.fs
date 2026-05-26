namespace SpectreTuff

open Spectre.Console

type Look = {
  Color: Color option
  BackgroundColor: Color option
  Decorations: Decoration list
}

module Look =
  let empty = {
    Color = None
    BackgroundColor = None
    Decorations = []
  }

  let fromColor color = { empty with Color = Some color }

  let fromBackgroundColor color = {
    empty with
        BackgroundColor = Some color
  }

  let fromDecorations decorations = { empty with Decorations = decorations }

  let withColor color look = { look with Color = Some color }

  let withBackgroundColor color look = {
    look with
        BackgroundColor = Some color
  }

  let withDecorations decorations look = { look with Decorations = decorations }

  let toStyle look =
    let fg =
      match look.Color with
      | Some c -> System.Nullable c
      | None -> System.Nullable()

    let bg =
      match look.BackgroundColor with
      | Some c -> System.Nullable c
      | None -> System.Nullable()

    let dec =
      match look.Decorations with
      | [] -> Decoration.None
      | [ d ] -> d
      | ds -> ds |> List.reduce (|||)

    Style(fg, bg, dec)
