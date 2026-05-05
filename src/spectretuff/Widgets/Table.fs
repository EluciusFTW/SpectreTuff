namespace SpectreTuff.Widgets

open Spectre.Tui
open System.Collections.Generic

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

  let tableWidget (columns: TableColumn seq) (rows: 't seq) =
    let widget = TableWidget<'t> (List<'t> rows)
    widget.Columns.AddRange columns
    widget

  let withHighlightStyle style (table: TableWidget<'t>) =
    table.HighlightStyle <- style
    table

  let withHeaderStyle style (table: TableWidget<'t>) =
    table.HeaderStyle <- style
    table

  let withShowHeader show (table: TableWidget<'t>) =
    table.ShowHeader <- show
    table

  let withShowSeparator show (table: TableWidget<'t>) =
    table.ShowSeparator <- show
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

  let withWrapAround enable (table: TableWidget<'t>) =
    table.WrapAround <- enable
    table

  let selectedIndex index table =
    withSelectedIndex (Some index) table
