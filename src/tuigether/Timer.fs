module Timer

open System
open System.Runtime.InteropServices
open Elmish
open Firebase.Database
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Widgets

// ─── Types ───────────────────────────────────────────────────────────────────

type Phase =
  | Work
  | Break

type TimerState =
  | Idle
  | Running
  | Paused
  | Flashing of int
  | Breaking of int

type Persistence = {
  Client: FirebaseClient
  SessionId: string
}

type Model = {
  Remaining: TimeSpan
  Phase: Phase
  State: TimerState
  ActiveDriver: string option
  ConnectedUsers: string list
  UserAvatars: Map<string, Creature>
  TickEpoch: int
  Persistence: Persistence
}

type Msg =
  | Start
  | Stop
  | Pause
  | Tick of int
  | Reset
  | SwitchDriver
  | SkipTimer
  | SkipPause
  | WorkFinished
  | FlashTick
  | StartBreak
  | BreakTick
  | BreakFinished
  | SessionUpdated of string list * string option * Map<string, Creature>
  | RemoteStateLoaded of Session.TimerState option
  | StateSaved

// ─── Constants ───────────────────────────────────────────────────────────────

let private workDuration = TimeSpan.FromMinutes 25.0
let private breakDuration = TimeSpan.FromMinutes 5.0
let private flashFrameCount = 6

// ─── Commands ────────────────────────────────────────────────────────────────

let private tickCmd (epoch: int) =
  Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 1000 }) () (fun () -> Tick epoch)

let private flashTickCmd = Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 200 }) () (fun () -> FlashTick)

let private breakTickCmd = Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 500 }) () (fun () -> BreakTick)

// ─── Notification ────────────────────────────────────────────────────────────

let private windowsToastScript (title: string) (message: string) =
  let escape (value: string) =
    value.Replace("'", "''")

  sprintf
    "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); $texts = $template.GetElementsByTagName('text'); $texts.Item(0).AppendChild($template.CreateTextNode('%s')) | Out-Null; $texts.Item(1).AppendChild($template.CreateTextNode('%s')) | Out-Null; $toast = [Windows.UI.Notifications.ToastNotification]::new($template); [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('tuigether').Show($toast)"
    (escape title)
    (escape message)

let private sendNotification (title: string) (message: string) =
  try
    let psi = Diagnostics.ProcessStartInfo()

    match RuntimeInformation.IsOSPlatform OSPlatform.OSX, RuntimeInformation.IsOSPlatform OSPlatform.Linux with
    | true, _ ->
      psi.FileName <- "osascript"
      psi.ArgumentList.Add("-e")
      psi.ArgumentList.Add(sprintf "display notification \"%s\" with title \"%s\"" message title)
    | _, true ->
      psi.FileName <- "notify-send"
      psi.ArgumentList.Add(title)
      psi.ArgumentList.Add(message)
    | _ ->
      psi.FileName <- "powershell"
      psi.ArgumentList.Add("-NoProfile")
      psi.ArgumentList.Add("-Command")
      psi.ArgumentList.Add(windowsToastScript title message)

    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    Diagnostics.Process.Start(psi) |> ignore
  with _ ->
    ()

// ─── Init ────────────────────────────────────────────────────────────────────

let init (client: FirebaseClient) (sessionId: string) = {
  Remaining = workDuration
  Phase = Work
  State = Idle
  ActiveDriver = None
  ConnectedUsers = []
  UserAvatars = Map.empty
  TickEpoch = 0
  Persistence = {
    Client = client
    SessionId = sessionId
  }
}

let resetForDriver (previous: Model) (driver: string option) (users: string list) (avatars: Map<string, Creature>) = {
  Remaining = workDuration
  Phase = Work
  State = Idle
  ActiveDriver = driver
  ConnectedUsers = users
  UserAvatars = avatars
  TickEpoch = previous.TickEpoch + 1
  Persistence = previous.Persistence
}

// ─── Persistence ─────────────────────────────────────────────────────────────

let private toTimerState (model: Model) : Session.TimerState = {
  RemainingSeconds = int model.Remaining.TotalSeconds
  IsRunning = model.State = Running
}

let private saveCmd (model: Model) : Cmd<Msg> =
  Cmd.OfAsync.perform
    (fun () -> Firebase.Timer.save model.Persistence.Client model.Persistence.SessionId (toTimerState model))
    ()
    (fun () -> StateSaved)

// ─── Update ──────────────────────────────────────────────────────────────────

let update msg model =
  match msg with
  | Start ->
    match model.State with
    | Idle
    | Paused ->
      match model.State with
      | Idle -> sendNotification "tuigether" "Work started!"
      | _ -> sendNotification "tuigether" "Work resumed"

      let epoch = model.TickEpoch + 1

      let m = {
        model with
            State = Running
            TickEpoch = epoch
      }

      m, Cmd.batch [ tickCmd epoch; saveCmd m ]
    | _ -> model, []
  | Stop ->
    sendNotification "tuigether" "Timer stopped"

    let m = {
      model with
          State = Idle
          TickEpoch = model.TickEpoch + 1
    }

    m, saveCmd m
  | Pause ->
    match model.State with
    | Running ->
      sendNotification "tuigether" "Paused"

      let m = {
        model with
            State = Paused
            TickEpoch = model.TickEpoch + 1
      }

      m, saveCmd m
    | _ -> model, []
  | Tick epoch when epoch <> model.TickEpoch -> model, []
  | Tick _ ->
    match model.State with
    | Running ->
      let next = model.Remaining - TimeSpan.FromSeconds 1.0

      if next <= TimeSpan.Zero then
        { model with Remaining = TimeSpan.Zero }, Cmd.ofMsg WorkFinished
      else
        { model with Remaining = next }, tickCmd model.TickEpoch
    | _ -> model, []
  | WorkFinished ->
    sendNotification "tuigether" "Drive finished — driver change, break started!"

    let m = {
      model with
          State = Flashing flashFrameCount
    }

    m, Cmd.batch [ flashTickCmd; saveCmd m ]
  | SkipTimer ->
    match model.State with
    | Running
    | Paused ->
      sendNotification "tuigether" "Drive skipped — driver change, break started!"

      let m = {
        model with
            State = Flashing flashFrameCount
            Remaining = TimeSpan.Zero
      }

      m, Cmd.batch [ flashTickCmd; saveCmd m ]
    | _ -> model, []
  | FlashTick ->
    match model.State with
    | Flashing n when n > 0 -> { model with State = Flashing(n - 1) }, flashTickCmd
    | Flashing _ -> model, Cmd.ofMsg StartBreak
    | _ -> model, []
  | StartBreak ->
    {
      model with
          State = Breaking 0
          Phase = Break
          Remaining = breakDuration
    },
    breakTickCmd
  | BreakTick ->
    match model.State with
    | Breaking frame ->
      let next = model.Remaining - TimeSpan.FromSeconds 0.5

      if next <= TimeSpan.Zero then
        { model with Remaining = TimeSpan.Zero }, Cmd.ofMsg BreakFinished
      else
        {
          model with
              State = Breaking(frame + 1)
              Remaining = next
        },
        breakTickCmd
    | _ -> model, []
  | BreakFinished ->
    sendNotification "tuigether" "Break over!"

    let m = {
      model with
          State = Idle
          Phase = Work
          Remaining = workDuration
    }

    m, saveCmd m
  | SkipPause ->
    match model.State with
    | Breaking _ ->
      sendNotification "tuigether" "Break skipped!"

      let m = {
        model with
            State = Idle
            Phase = Work
            Remaining = workDuration
      }

      m, saveCmd m
    | _ -> model, []
  | Reset ->
    match model.State with
    | Idle
    | Paused ->
      {
        model with
            Remaining = workDuration
            Phase = Work
            State = Idle
      },
      []
    | _ -> model, []
  | SwitchDriver -> model, []
  | SessionUpdated(users, driver, avatars) ->
    {
      model with
          ConnectedUsers = users
          ActiveDriver = driver
          UserAvatars = avatars
    },
    []
  | RemoteStateLoaded(Some state) ->
    let remaining = TimeSpan.FromSeconds(float state.RemainingSeconds)
    let withRemaining = { model with Remaining = remaining }

    match state.IsRunning, model.State with
    | true, (Idle | Paused) -> withRemaining, Cmd.ofMsg Start
    | false, Running -> withRemaining, Cmd.ofMsg Pause
    | _ -> withRemaining, []
  | RemoteStateLoaded None -> model, []
  | StateSaved -> model, []

let subscriptions (model: Model) =
  Firebase.Timer.subscription model.Persistence.Client model.Persistence.SessionId RemoteStateLoaded

// ─── Widget ──────────────────────────────────────────────────────────────────

let private formatTime (t: TimeSpan) =
  sprintf "%02d:%02d" (int t.TotalMinutes) t.Seconds

// Road grid: N columns × 2 rows (N derived from viewport width at render time)
//
// R0:  .  .  .  .  [head]  .  .  .  .  .  .   sky row + driver head (car middle)
// R1:  ═  ═  ═  [whl][base][whl]  ─  ─  ─  ─   road + car wheels
//
// carPos ∈ [0, roadWidth-3]:  car occupies cols carPos..carPos+2
// cols < carPos  → filled road     cols > carPos+2 → unfilled road

let private carWidth = 3

let private styledBlock (color: Color) =
  Text.styledSpan (Nullable(Style color)) "██"

let private emptyBlock = Text.span "  "

// Pause glyph (big "||"): two vertical bars, 3 rows tall. The Timer gets only
// 4 rows here (journey height 7, minus box border and the panel keys strip), so
// glyph + timer line fill it exactly. Leading empty cell aligns with the "  " margin.

let private pauseRows =
  let e = Empty
  let P = Filled Color.DeepSkyBlue1

  [ [ e; P; P; e; P; P ]; [ e; P; P; e; P; P ]; [ e; P; P; e; P; P ] ]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let renderCell cell =
          match cell with
          | Empty -> emptyBlock
          | Filled color -> styledBlock color

        match model.State with
        | Breaking _ ->
          let pauseLines = pauseRows |> List.map (fun row -> row |> List.map renderCell |> Text.line)

          let infoLine = Text.line [ Text.span (sprintf "  BREAK  %s" (formatTime model.Remaining)) ]

          context.Render(paragraph (pauseLines @ [ infoLine ]), context.Viewport)

        | _ ->
          let roadWidth = max carWidth (context.Viewport.Width / 2)

          let driverColor =
            model.ActiveDriver
            |> Option.bind (fun u -> model.UserAvatars |> Map.tryFind u)
            |> Option.map (fun creature ->
              creature.SmallRows
              |> List.concat
              |> List.tryPick (function
                | Filled c -> Some c
                | Empty -> None)
              |> Option.defaultValue Color.Silver)

          let filledColor =
            match model.State with
            | Running -> Color.Green
            | Paused -> Color.Yellow
            | Flashing n when n % 2 = 0 -> Color.Red
            | Flashing _ -> Color.Yellow1
            | _ -> Color.Grey35

          let totalSecs =
            match model.Phase with
            | Work -> workDuration.TotalSeconds
            | Break -> breakDuration.TotalSeconds

          let progress = 1.0 - model.Remaining.TotalSeconds / totalSecs

          let carPos =
            match model.State with
            | Flashing _ -> roadWidth - carWidth
            | _ -> min (roadWidth - carWidth) (int (progress * float (roadWidth - carWidth)))

          let isFinish =
            match model.State with
            | Flashing _ -> true
            | _ -> false

          let roadCell row col =
            let inCar = col >= carPos && col < carPos + carWidth
            let filled = col < carPos
            let finish = isFinish && col = roadWidth - 1
            let isHead = col - carPos = 1

            let roadSurface =
              match filled with
              | true -> styledBlock filledColor
              | false -> styledBlock Color.Grey23

            match row with
            | 0 ->
              match inCar && isHead, finish with
              | true, _ -> styledBlock (driverColor |> Option.defaultValue Color.Silver)
              | false, true -> styledBlock Color.White
              | false, false -> emptyBlock
            | _ ->
              match finish, inCar, isHead with
              | true, _, _ -> styledBlock Color.White
              | false, true, true -> styledBlock Color.Grey
              | false, true, false -> styledBlock Color.Grey3
              | false, false, _ -> roadSurface

          let stateStr =
            match model.State with
            | Running -> "▶"
            | Paused -> "||"
            | Flashing _ -> "!!!"
            | _ -> "■"

          let roadLines = [
            for row in 0..1 -> Text.line [ for col in 0 .. (roadWidth - 1) -> roadCell row col ]
          ]

          let infoLine = Text.line [ Text.span (sprintf "  %s %s" stateStr (formatTime model.Remaining)) ]

          context.Render(paragraph (roadLines @ [ infoLine ]), context.Viewport)
  }
