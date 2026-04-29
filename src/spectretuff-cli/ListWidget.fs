module ListWidget

open Spectre.Console
open SpectreTuff.Widgets
open Spectre.Tui

type Model = { index: int; items: ListItem list }

type Msg =
    | Up
    | Down
    | Delete
    | Add

let update msg model =
    let itemCount = model.items.Length

    if itemCount = 0 then
        model, []
    else
        match msg with
        | Up ->
            let nextIndex = (model.index - 1 + itemCount) % itemCount
            { model with index = nextIndex }, []
        | Down ->
            let nextIndex = (model.index + 1) % itemCount
            { model with index = nextIndex }, []
        | Delete ->
            { model with
                items = model.items |> List.removeAt model.index
                index = (model.index + 1) % model.items.Length - 1 },
            []
        | Add ->
            { model with
                items = model.items |> List.insertAt model.index (ListItem "Added Item")
                index = (model.index + 1) % model.items.Length - 1 },
            []

let view (renderer: Renderer) (model: Model) dispatch =
    renderer.Draw(fun ctx elapsed ->
        let vp = ctx.Viewport

        let listW =
            listWidget model.items
            |> selectedIndex model.index
            |> withHighlightSymbol (LineExtensions.FromString("> ", Style(Color.Blue)))
            |> withWrapAround true

        let listArea = Rectangle(vp.X + 2, vp.Y + 2, 30, 10)
        RenderContextExtensions.Render(ctx, listW, listArea)

        let info = $"Selected Index: {model.index} (of {model.items.Length})"

        RenderContextExtensions.Render(
            ctx,
            Text(LineExtensions.FromString(info, Style(Color.Green))),
            Rectangle(vp.X + 2, vp.Y + 13, 30, 1)
        ))

