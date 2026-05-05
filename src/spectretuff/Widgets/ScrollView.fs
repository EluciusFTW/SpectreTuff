namespace SpectreTuff.Widgets

open Spectre.Tui

[<AutoOpen>]
module ScrollViews =

  let scrollView (inner: IWidget) =
    ScrollViewWidget (Inner = inner)

  let withVerticalScroll mode (scrollView: ScrollViewWidget) =
    scrollView.VerticalScroll <- mode
    scrollView

  let withHorizontalScroll mode (scrollView: ScrollViewWidget) =
    scrollView.HorizontalScroll <- mode
    scrollView

  let withContentSize width height (scrollView: ScrollViewWidget) =
    scrollView.ContentSize <- System.Nullable (Size (width, height))
    scrollView

  let withScrollbarStyle style (scrollView: ScrollViewWidget) =
    scrollView.ScrollbarStyle <- style
    scrollView

  let withScrollbarThumbStyle style (scrollView: ScrollViewWidget) =
    scrollView.ScrollbarThumbStyle <- style
    scrollView
