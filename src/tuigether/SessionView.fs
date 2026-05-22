module SessionView

open System
open Elmish
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Layout
open SpectreTuff.Widgets

type Model = {
  SessionId: string
  SessionData: Session.Data
  User: string
  Status: string
}

type Msg =
  | GoBack
  | JoinCompleted of Result<unit, string>

let init (user: string) (sessionId: string) (sessionData: Session.Data) = {
  SessionId = sessionId
  SessionData = sessionData
  User = user
  Status = "joining…"
}

let update msg model =
  match msg with
  | GoBack -> model, []
  | JoinCompleted(Ok()) -> { model with Status = "connected" }, []
  | JoinCompleted(Error e) ->
    {
      model with
          Status = sprintf "join error: %s" e
    },
    []

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  let data = model.SessionData

  let users =
    if isNull data.ConnectedUsers || data.ConnectedUsers.Count = 0 then
      "(none)"
    else
      data.ConnectedUsers.Keys |> String.concat ", "

  let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm:ss")

  let content =
    sprintf
      "  Goal:    %s\n  Started: %s\n  Users:   %s\n  Status:  %s\n\n  [esc/backspace] back  [q] quit"
      data.Goal
      startedAt
      users
      model.Status
    |> ofString

  ctx.Render(
    box (Look.fromColor Color.Green)
    |> withTitle (sprintf "Session: %s" model.SessionId)
    |> withInnerWidget content,
    area
  )
