namespace SpectreTuff.Widgets

open Spectre.Console
open Spectre.Tui
open System.Collections.Generic
open SpectreTuff

type ListItem(text: string) =
  interface IListWidgetItem with
    member _.CreateText(isSelected) =
      let style =
        if isSelected then
          Style(Color.Yellow, Color.Blue)
        else
          Style.Plain

      Text(LineExtensions.FromString(text, style))

[<AutoOpen>]
module Lists =
  let list<'t when 't :> IListWidgetItem> (items: 't seq) =
    ListWidget<'t>(List<'t>(items))

  let withHighlightLook look (list: ListWidget<'t>) =
    list.HighlightStyle <- Look.toStyle look
    list

  let withHighlightSymbol symbol (list: ListWidget<'t>) =
    list.HighlightSymbol <- symbol
    list

  let withSelectedIndex (index: int option) (list: ListWidget<'t>) =
    list.SelectedIndex <-
      match index with
      | Some i -> System.Nullable i
      | None -> System.Nullable()

    list

  let wrapAround (list: ListWidget<'t>) =
    list.WrapAround <- true
    list

  let noWrapAround (list: ListWidget<'t>) =
    list.WrapAround <- false
    list

  let selectedIndex index list =
    withSelectedIndex (Some index) list
