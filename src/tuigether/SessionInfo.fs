module SessionInfo

open System
open Spectre.Tui
open Keymap
open SpectreTuff.Widgets

type Model = {
  SessionId: string
  SessionData: Session.Data
  Status: string
}

type Msg = | NoOp

let init (sessionId: string) (sessionData: Session.Data) (status: string) = {
  SessionId = sessionId
  SessionData = sessionData
  Status = status
}

let update _msg model =
  model, []

let private bindings: KeyBinding<Model, Msg> list = []

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

let widget (model: Model) : IWidget =
  let data = model.SessionData
  let startedAt = DateTimeOffset.FromUnixTimeMilliseconds(data.StartedAt).ToString("yyyy-MM-dd HH:mm:ss")

  sprintf "  Goal:    %s\n  ID:      %s\n  Started: %s\n  Status:  %s" data.Goal model.SessionId startedAt model.Status
  |> ofString
  :> IWidget
