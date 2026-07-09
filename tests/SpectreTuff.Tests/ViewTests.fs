module SpectreTuff.Tests.ViewTests

open Expecto
open Spectre.Tui
open SpectreTuff.View

[<Tests>]
let tests =
  testList "View reducers" [
    testList "intersectAll" [
      test "single rectangle is returned unchanged" {
        let r = Rectangle(3, 4, 10, 20)
        Expect.equal (intersectAll [ r ]) r "identity on singleton"
      }

      test "two rectangles reduce to their overlap" {
        let a = Rectangle(0, 0, 10, 10)
        let b = Rectangle(5, 5, 10, 10)
        Expect.equal (intersectAll [ a; b ]) (Rectangle(5, 5, 5, 5)) "overlap region"
      }

      test "overlap shrinks with each additional rectangle" {
        let rects = [ Rectangle(0, 0, 10, 10); Rectangle(5, 5, 10, 10); Rectangle(0, 0, 8, 8) ]
        Expect.equal (intersectAll rects) (Rectangle(5, 5, 3, 3)) "progressive intersection"
      }

      test "empty sequence throws" {
        Expect.throws (fun () -> intersectAll [] |> ignore) "Seq.reduce has no seed for empty input"
      }
    ]

    testList "boundingBoxOf" [
      test "single rectangle is returned unchanged" {
        let r = Rectangle(3, 4, 10, 20)
        Expect.equal (boundingBoxOf [ r ]) r "identity on singleton"
      }

      test "two disjoint rectangles reduce to their union" {
        let a = Rectangle(0, 0, 5, 5)
        let b = Rectangle(10, 10, 5, 5)
        Expect.equal (boundingBoxOf [ a; b ]) (Rectangle(0, 0, 15, 15)) "enclosing box"
      }

      test "union grows to contain every rectangle" {
        let rects = [ Rectangle(2, 2, 4, 4); Rectangle(0, 8, 3, 3); Rectangle(9, 0, 1, 1) ]
        Expect.equal (boundingBoxOf rects) (Rectangle(0, 0, 10, 11)) "covers all"
      }

      test "empty sequence throws" {
        Expect.throws (fun () -> boundingBoxOf [] |> ignore) "Seq.reduce has no seed for empty input"
      }
    ]
  ]
