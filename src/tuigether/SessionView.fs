module SessionView

open System
open Spectre.Tui
open Keymap
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

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (SpecialKey ConsoleKey.Backspace) (fun _ -> {
    Description = "back"
    Message = Some GoBack
  })
  KeyBinding.dynamic (SpecialKey ConsoleKey.Escape) (fun _ -> {
    Description = "back"
    Message = Some GoBack
  })
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) : IWidget =
  let data = model.SessionData

  let users =
    if isNull data.ConnectedUsers || data.ConnectedUsers.Count = 0 then
      "(none)"
    else
      data.ConnectedUsers.Keys |> String.concat ", "

  let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm:ss")

  sprintf "  Goal:    %s\n  Started: %s\n  Users:   %s\n  Status:  %s" data.Goal startedAt users model.Status
  |> ofString
  :> IWidget
