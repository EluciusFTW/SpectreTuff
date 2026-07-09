module SpectreTuff.Tests.LookTests

open Expecto
open Spectre.Console
open SpectreTuff.Look

[<Tests>]
let tests =
  testList "Look.toStyle" [
    test "empty look has no colors and no decoration" {
      let style = toStyle empty
      Expect.equal style.Foreground Color.Default "foreground defaults"
      Expect.equal style.Background Color.Default "background defaults"
      Expect.equal style.Decoration Decoration.None "no decoration"
    }

    test "foreground color is carried through" {
      let style = empty |> withColor Color.Red |> toStyle
      Expect.equal style.Foreground Color.Red "foreground set"
      Expect.equal style.Background Color.Default "background still default"
    }

    test "background color is carried through" {
      let style = empty |> withBackgroundColor Color.Blue |> toStyle
      Expect.equal style.Background Color.Blue "background set"
      Expect.equal style.Foreground Color.Default "foreground still default"
    }

    test "empty decoration list maps to Decoration.None" {
      let style = { empty with Decorations = [] } |> toStyle
      Expect.equal style.Decoration Decoration.None "none"
    }

    test "single decoration is used as-is" {
      let style =
        {
          empty with
              Decorations = [ Decoration.Bold ]
        }
        |> toStyle

      Expect.equal style.Decoration Decoration.Bold "bold"
    }

    test "multiple decorations are combined with bitwise-or" {
      let style =
        {
          empty with
              Decorations = [ Decoration.Bold; Decoration.Italic; Decoration.Underline ]
        }
        |> toStyle

      Expect.equal
        style.Decoration
        (Decoration.Bold ||| Decoration.Italic ||| Decoration.Underline)
        "all flags combined"
    }

    test "duplicate decorations collapse to the same flag" {
      let style =
        {
          empty with
              Decorations = [ Decoration.Bold; Decoration.Bold ]
        }
        |> toStyle

      Expect.equal style.Decoration Decoration.Bold "still just bold"
    }
  ]
