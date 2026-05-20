namespace SpectreTuff.Widgets

open Spectre.Tui
open System.Collections.Generic
open SpectreTuff

[<AutoOpen>]
module Tables =

  // Column

  let tableColumn (header: string) =
    TableColumn (TextLine (TextSpan header))

  let withWidth width (column: TableColumn) =
    column.Width <- width
    column

  let withHorizontalAlignment alignment (column: TableColumn) =
    column.Alignment <- alignment
    column

  let withVerticalAlignment alignment (column: TableColumn) =
    column.VerticalAlignment <- alignment
    column

  let withWrap wrap (column: TableColumn) =
    column.Wrap <- wrap
    column

  // Widget

  let table (columns: TableColumn seq) (rows: 't seq) =
    let widget = TableWidget<'t> (List<'t> rows)
    widget.Columns.AddRange columns
    widget

  let withHighlightLook look (table: TableWidget<'t>) =
    table.HighlightStyle <- Look.toStyle look
    table

  let withHeaderLook look (table: TableWidget<'t>) =
    table.HeaderStyle <- Look.toStyle look
    table

  let showHeader (table: TableWidget<'t>) =
    table.ShowHeader <- true
    table

  let hideHeader (table: TableWidget<'t>) =
    table.ShowHeader <- false
    table

  let showSeparator (table: TableWidget<'t>) =
    table.ShowSeparator <- true
    table

  let hideSeparator (table: TableWidget<'t>) =
    table.ShowSeparator <- false
    table

  let withColumnSpacing spacing (table: TableWidget<'t>) =
    table.ColumnSpacing <- spacing
    table

  let withSelectedIndex (index: int option) (table: TableWidget<'t>) =
    table.SelectedIndex <-
      match index with
      | Some i -> System.Nullable i
      | None -> System.Nullable ()
    table

  let wrapAround (table: TableWidget<'t>) =
    table.WrapAround <- true
    table

  let noWrapAround (table: TableWidget<'t>) =
    table.WrapAround <- false
    table

  let selectedIndex index table =
    withSelectedIndex (Some index) table
