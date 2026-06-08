[<AutoOpen>]
module SpectreTuff.Widgets.ScrollViews

open Spectre.Tui
open SpectreTuff.Look

let scrollView (inner: IWidget) =
  ScrollViewWidget(Inner = inner)

let withVerticalScroll mode (scrollView: ScrollViewWidget) =
  scrollView.VerticalScroll <- mode
  scrollView

let withHorizontalScroll mode (scrollView: ScrollViewWidget) =
  scrollView.HorizontalScroll <- mode
  scrollView

let withContentSize width height (scrollView: ScrollViewWidget) =
  scrollView.ContentSize <- System.Nullable(Size(width, height))
  scrollView

let withScrollbarLook look (scrollView: ScrollViewWidget) =
  scrollView.ScrollbarStyle <- toStyle look
  scrollView

let withScrollbarThumbLook look (scrollView: ScrollViewWidget) =
  scrollView.ScrollbarThumbStyle <- toStyle look
  scrollView
