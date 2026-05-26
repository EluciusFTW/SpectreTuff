namespace SpectreTuff.Widgets

open Spectre.Tui
open SpectreTuff

[<AutoOpen>]
module ScrollViews =

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
    scrollView.ScrollbarStyle <- Look.toStyle look
    scrollView

  let withScrollbarThumbLook look (scrollView: ScrollViewWidget) =
    scrollView.ScrollbarThumbStyle <- Look.toStyle look
    scrollView
