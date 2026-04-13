namespace SpectreTuff

open Spectre.Tui

module Layout =
    type Direction = Horizontal | Vertical

    let layout name =
        new Layout(name)

    let split direction children (layout: Layout) =
        match direction with
        | Horizontal -> layout.SplitRows children
        | Vertical -> layout.SplitColumns children

    let splitHorizontally = split Horizontal
    let splitVertically = split Vertical

    // This is still puzzling - why pass the rendercontext to Get,
    // instead of a Rectangle (nothing more that the vctx.Viewport is used in Get).
    // This way, when Get is called on a child Layout, the Wiewport does not align anymore.
    // But maybe that is on design, to be able to zoom into sublayouts.
    let getPortFor ctx name (layout: Layout) =
        layout.GetArea(ctx, name)

    let getLayout name (layout: Layout) =
        layout.GetLayout name

    let setVisibility value (layout: Layout) =
        layout.IsVisible <- value
        layout

    let show = setVisibility true
    let hide = setVisibility false
