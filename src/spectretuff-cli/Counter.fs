module Counter

open System
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.View
open SpectreTuff.Widgets

type Model = { Count: int }

type Msg =
  | Increment of int
  | Decrement

let handleKey (key: ConsoleKeyInfo) : Msg option =
  match key.KeyChar with
  | '+' -> Some(Increment 1)
  | '-' -> Some Decrement
  | _ -> None

let update msg model =
  match msg with
  | Increment n -> { model with Count = model.Count + n }, []
  | Decrement -> { model with Count = model.Count - 1 }, []

let private counterInfo model =
  $""" 
  Increase the count by pressing 1,2,5. 

  Current Count: {model.Count}
  """
  |> textBox
  |> withMode TextBoxMode.MultiLine

let widget model =
  box (Look.fromColor Color.Purple)
  |> withTitle "Inner Box"
  |> withInnerWidget (counterInfo model)
