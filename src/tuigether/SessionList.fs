module SessionList

open System
open Elmish
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  Sessions: (string * Session.Data) list
  SelectedIndex: int
  Status: string
}

type Msg =
  | Up
  | Down
  | OpenSelected
  | CreateNew
  | SessionsLoaded of (string * Session.Data) list
  | SessionChanged of string * Session.Data
  | SessionRemoved of string
  | LoadError of string

let init () =
  {
    Sessions = []
    SelectedIndex = 0
    Status = "loading…"
  },
  []

let private sortSessions sessions =
  sessions
  |> List.sortByDescending (fun (_, data: Session.Data) -> data.StartedAt)

let update msg model =
  match msg with
  | Up ->
    {
      model with
          SelectedIndex = max 0 (model.SelectedIndex - 1)
    },
    []
  | Down ->
    {
      model with
          SelectedIndex = min (List.length model.Sessions - 1) (model.SelectedIndex + 1)
    },
    []
  | OpenSelected -> model, []
  | CreateNew -> model, []
  | SessionsLoaded sessions ->
    {
      model with
          Sessions = sortSessions sessions
          Status = "connected"
    },
    []
  | SessionChanged(id, data) when not (String.IsNullOrEmpty id) && not (isNull (data :> obj)) ->
    let sessions =
      model.Sessions
      |> List.filter (fun (k, _) -> k <> id)
      |> List.append [ id, data ]

    {
      model with
          Sessions = sortSessions sessions
    },
    []
  | SessionChanged _ -> model, []
  | SessionRemoved id ->
    {
      model with
          Sessions = model.Sessions |> List.filter (fun (k, _) -> k <> id)
    },
    []
  | LoadError e ->
    {
      model with
          Status = sprintf "error: %s" e
    },
    []

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  let title = sprintf "Sessions  [%s]  — [↑↓] navigate  [enter] open  [n] new  [q] quit" model.Status

  let innerWidget: IWidget =
    if model.Sessions.IsEmpty then
      ofString "No sessions yet. Press [n] to create one." :> IWidget
    else
      let items =
        model.Sessions
        |> List.choose (fun (_, data) ->
          if isNull (data :> obj) then
            None
          else
            let userCount =
              if isNull data.ConnectedUsers then
                0
              else
                data.ConnectedUsers.Count

            let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm")

            Some(ListItem(sprintf "%-40s  %s  %d user(s)" data.Goal startedAt userCount)))

      list items |> selectedIndex model.SelectedIndex |> wrapAround :> IWidget

  ctx.Render(
    box (Look.fromColor Color.Cyan)
    |> withTitle title
    |> withInnerWidget innerWidget,
    area
  )
