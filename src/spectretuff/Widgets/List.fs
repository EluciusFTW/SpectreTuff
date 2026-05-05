namespace SpectreTuff.Widgets

open Spectre.Console
open Spectre.Tui
open System.Collections.Generic

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
module ListWidgetFSharp =
  let listWidget<'t when 't :> IListWidgetItem> (items: 't seq) =
    ListWidget<'t>(List<'t>(items))

  let withHighlightStyle style (list: ListWidget<'t>) =
    list.HighlightStyle <- style
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

  let withWrapAround (enable: bool) (list: ListWidget<'t>) =
    list.WrapAround <- enable
    list

  let selectedIndex index list =
    withSelectedIndex (Some index) list
