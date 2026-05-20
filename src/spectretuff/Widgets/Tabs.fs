namespace SpectreTuff.Widgets

open Spectre.Tui
open System.Collections.Generic
open SpectreTuff

type TabItem(text: string) =
  interface ITabWidgetItem with
    member _.CreateTextLine(_isSelected) =
      TextLine(TextSpan(text))

[<AutoOpen>]
module Tabs =
  let tabs<'t when 't :> ITabWidgetItem> (items: 't seq) =
    TabsWidget<'t>(List<'t>(items))

  let withHighlightLook look (tabs: TabsWidget<'t>) =
    tabs.HighlightStyle <- Look.toStyle look
    tabs

  let withSeparator (separator: TextSpan) (tabs: TabsWidget<'t>) =
    tabs.Separator <- separator
    tabs

  let withPadding (left: int) (right: int) (tabs: TabsWidget<'t>) =
    tabs.LeftPadding <- TextLine(TextSpan(System.String(' ', left)))
    tabs.RightPadding <- TextLine(TextSpan(System.String(' ', right)))
    tabs

  let withSelectedIndex (index: int option) (tabs: TabsWidget<'t>) =
    match index with
    | Some i -> tabs.SelectedIndex <- i
    | None -> ()
    tabs

  let selectedIndex index tabs =
    withSelectedIndex (Some index) tabs
