module AvatarDemo

open System
open Spectre.Console
open Spectre.Tui
open Keymap
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = { Index: int }

type Msg =
  | Up
  | Down

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" Up
  KeyBinding.createSpecial ConsoleKey.DownArrow "down" Down
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let update msg model =
  let count = library.Length

  match msg with
  | Up ->
    {
      model with
          Index = (model.Index - 1 + count) % count
    },
    []
  | Down ->
    {
      model with
          Index = (model.Index + 1) % count
    },
    []

let keyMap model =
  KeyBinding.toKeyMap bindings model

let private innerLayout =
  layout "avatar-inner"
  |> splitVertically [| layout "list" |> withRatio 1; layout "preview" |> withRatio 2 |]

let widget (model: Model) =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let port = getPort context.Viewport innerLayout
        let items = library |> List.map (fun c -> ListItem c.Name)

        let listWidget =
          list items
          |> selectedIndex model.Index
          |> withHighlightSymbol (LineExtensions.FromString("> ", Style Color.Cyan))
          |> wrapAround
          :> IWidget

        context.Render(listWidget, port "list")
        context.Render(avatarByIndex model.Index, port "preview")
  }

let init () = { Index = 0 }
