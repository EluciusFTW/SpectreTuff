module Log

open Spectre.Tui
open SpectreTuff.Layout
open SpectreTuff.View
open SpectreTuff.Widgets

type Model = { Entries: ResizeArray<string> }

let init () = { Entries = ResizeArray<string>() }

let append (entry: string) (model: Model) =
  model.Entries.Add entry

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  let content =
    model.Entries
    |> Seq.map (fun entry -> TextLine (TextSpan entry))
    |> paragraph
  let logView = scrollView content
  logView.ScrollToBottom ()
  ctx.Render (
    box Spectre.Console.Color.Grey |> withTitle "Log" |> withInnerWidget logView,
    area)
