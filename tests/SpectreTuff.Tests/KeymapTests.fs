module SpectreTuff.Tests.KeymapTests

open System
open Expecto
open Spectre.Tui
open SpectreTuff.Keymap

type private Msg =
  | Up
  | Quit
  | Save

let private charKey (c: char) =
  ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)

let private specialKey (key: ConsoleKey) =
  ConsoleKeyInfo(char 0, key, false, false, false)

// `Save` is only enabled while the model (a "dirty" flag) is true.
let private bindings: KeyBinding<bool, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  |> KeyBinding.orKey (CharKey 'k')
  KeyBinding.create 'q' "quit" Quit
  KeyBinding.dynamic (CharKey 's') (fun dirty -> {
    Description = "save"
    Message = if dirty then Some Save else None
  })
]

[<Tests>]
let tests =
  testList "Keymap" [
    testList "KeyBinding.handleKey" [
      test "matches a char key" { Expect.equal (KeyBinding.handleKey bindings (charKey 'q') false) (Some Quit) "quit" }

      test "matches a special key" {
        Expect.equal (KeyBinding.handleKey bindings (specialKey ConsoleKey.UpArrow) false) (Some Up) "up"
      }

      test "matches an alias added with orKey" {
        Expect.equal (KeyBinding.handleKey bindings (charKey 'k') false) (Some Up) "alias"
      }

      test "char matching is case-sensitive" {
        Expect.equal (KeyBinding.handleKey bindings (charKey 'Q') false) None "upper case Q is unbound"
      }

      test "unbound key yields None" { Expect.equal (KeyBinding.handleKey bindings (charKey 'x') false) None "unbound" }

      test "disabled binding yields None" {
        Expect.equal (KeyBinding.handleKey bindings (charKey 's') false) None "disabled"
        Expect.equal (KeyBinding.handleKey bindings (charKey 's') true) (Some Save) "enabled"
      }
    ]

    testList "KeyBinding.helpEntries" [
      test "lists enabled bindings, aliases joined" {
        Expect.equal (KeyBinding.helpEntries bindings false) [ "↑/k", "up"; "q", "quit" ] "save hidden"
      }

      test "includes dynamic bindings once enabled" {
        Expect.contains (KeyBinding.helpEntries bindings true) ("s", "save") "save shown"
      }
    ]

    testList "KeyBinding.toKeyMap" [
      test "one help entry per enabled binding, with all its keys" {
        let help = (KeyBinding.toKeyMap bindings false).Help() |> Seq.toList
        Expect.equal (help |> List.map _.Help) [ "up"; "quit" ] "descriptions"
        Expect.equal help.Head.Keys.Count 2 "up carries its alias"
      }
    ]

    testList "KeyTrigger" [
      test "toKeyPress maps Spacebar" {
        Expect.equal (KeyTrigger.toKeyPress (SpecialKey ConsoleKey.Spacebar)).Key Key.Space "space"
      }

      test "displayName" {
        Expect.equal (KeyTrigger.displayName (CharKey 'x')) "x" "char as bound"
        Expect.equal (KeyTrigger.displayName (SpecialKey ConsoleKey.Escape)) "Esc" "escape"
        Expect.equal (KeyTrigger.displayName (SpecialKey ConsoleKey.PageDown)) "PgDn" "page down"
      }
    ]
  ]
