module Timer

open System
open Elmish
open Spectre.Console
open Spectre.Tui
open Keymap
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

type Model = {
  Remaining: TimeSpan
  Phase: Phase
  State: TimerState
  ActiveDriver: string option
  ConnectedUsers: string list
  UserAvatars: Map<string, Creature>
}

type Msg =
  | Start
  | Stop
  | Pause
  | Tick
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

// ─── Constants ───────────────────────────────────────────────────────────────

let private workDuration = TimeSpan.FromMinutes 25.0
let private breakDuration = TimeSpan.FromMinutes 5.0
let private flashFrameCount = 6

// ─── Commands ────────────────────────────────────────────────────────────────

let private tickCmd = Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 1000 }) () (fun () -> Tick)

let private flashTickCmd = Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 200 }) () (fun () -> FlashTick)

let private breakTickCmd = Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep 500 }) () (fun () -> BreakTick)

// ─── Notification ────────────────────────────────────────────────────────────

let private sendNotification () =
  try
    let psi = Diagnostics.ProcessStartInfo()
    psi.FileName <- "osascript"
    psi.ArgumentList.Add("-e")
    psi.ArgumentList.Add("display notification \"Fahrerwechsel!\" with title \"tuigether\"")
    psi.UseShellExecute <- false
    Diagnostics.Process.Start(psi) |> ignore
  with _ ->
    ()

// ─── Helpers ─────────────────────────────────────────────────────────────────

let private nextInList (users: string list) (current: string option) =
  match users with
  | [] -> None
  | _ ->
    let idx =
      current
      |> Option.bind (fun c -> users |> List.tryFindIndex ((=) c))
      |> Option.defaultValue -1

    Some users.[(idx + 1) % users.Length]

// ─── Init ────────────────────────────────────────────────────────────────────

let init () = {
  Remaining = workDuration
  Phase = Work
  State = Idle
  ActiveDriver = None
  ConnectedUsers = []
  UserAvatars = Map.empty
}

let resetForDriver (driver: string option) (users: string list) (avatars: Map<string, Creature>) = {
  Remaining = workDuration
  Phase = Work
  State = Idle
  ActiveDriver = driver
  ConnectedUsers = users
  UserAvatars = avatars
}

// ─── Update ──────────────────────────────────────────────────────────────────

let update msg model =
  match msg with
  | Start ->
    match model.State with
    | Idle
    | Paused -> { model with State = Running }, tickCmd
    | _ -> model, []
  | Stop -> { model with State = Idle }, []
  | Pause ->
    match model.State with
    | Running -> { model with State = Paused }, []
    | _ -> model, []
  | Tick ->
    match model.State with
    | Running ->
      let next = model.Remaining - TimeSpan.FromSeconds 1.0

      if next <= TimeSpan.Zero then
        { model with Remaining = TimeSpan.Zero }, Cmd.ofMsg WorkFinished
      else
        { model with Remaining = next }, tickCmd
    | _ -> model, []
  | WorkFinished ->
    sendNotification ()

    {
      model with
          State = Flashing flashFrameCount
    },
    flashTickCmd
  | SkipTimer ->
    match model.State with
    | Running
    | Paused ->
      sendNotification ()

      {
        model with
            State = Flashing flashFrameCount
            Remaining = TimeSpan.Zero
      },
      flashTickCmd
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
    {
      model with
          State = Idle
          Phase = Work
          Remaining = workDuration
    },
    []
  | SkipPause ->
    match model.State with
    | Breaking _ ->
      {
        model with
            State = Idle
            Phase = Work
            Remaining = workDuration
      },
      []
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

// ─── Key bindings ────────────────────────────────────────────────────────────

let private bindings: KeyBinding<Model, Msg> list = [
  KeyBinding.dynamic (CharKey 's') (fun model ->
    match model.State with
    | Running -> {
        Description = "pause"
        Message = Some Pause
      }
    | Idle
    | Paused -> {
        Description = "start"
        Message = Some Start
      }
    | _ -> { Description = ""; Message = None })
  KeyBinding.dynamic (CharKey 'j') (fun model ->
    match model.State with
    | Running
    | Paused -> {
        Description = "skip timer"
        Message = Some SkipTimer
      }
    | Breaking _ -> {
        Description = "skip pause"
        Message = Some SkipPause
      }
    | _ -> { Description = ""; Message = None })
  KeyBinding.dynamic (CharKey 'r') (fun model -> {
    Description = "reset"
    Message =
      match model.State with
      | Idle
      | Paused -> Some Reset
      | _ -> None
  })
  KeyBinding.create 'n' "next driver" SwitchDriver
]

let handleKey (key: ConsoleKeyInfo) (model: Model) : Msg option =
  KeyBinding.handleKey bindings key model

let keyMap model =
  KeyBinding.toKeyMap bindings model

// ─── Widget ──────────────────────────────────────────────────────────────────

let private formatTime (t: TimeSpan) =
  sprintf "%02d:%02d" (int t.TotalMinutes) t.Seconds

// Road grid: N columns × 4 rows (N derived from viewport width at render time)
//
// R0:  .  .  .  [car roof at carPos]  .  .  .   sky row
// R1:  ═  ═  ═  [wnd][body][wnd]  ─  ─  ─  ─   road + car body
// R2:  ═  ═  ═  [whl][base][whl]  ─  ─  ─  ─   road + car wheels
// R3:  .  .  .  .  .  .  .  .  .  .  .  .       empty / finish row
//
// carPos ∈ [0, roadWidth-3]:  car occupies cols carPos..carPos+2
// cols < carPos  → filled road     cols > carPos+2 → unfilled road

let private carWidth = 3

let private styledBlock (color: Color) =
  Text.styledSpan (Nullable(Style color)) "██"

let private emptyBlock = Text.span "  "

// Coffee cup pixel art: 8 cols × 9 rows (3 steam rows + 6 cup rows)
// Steam rows animate each break tick.
//
// Colors:  B=SandyBrown  K=Grey3(coffee)  S=Silver(steam/saucer/handle)  e=Empty
//
//      0   1   2   3   4   5   6   7
// S0:  .   .  [s]  .   .  [s]  .   .    steam
// S1:  .  [s]  .  [s] [s]  .  [s]  .    steam
// S2:  .   .  [s]  .   .  [s]  .   .    steam
// C0:  .  [B] [B] [B] [B] [B] [B]  .    top rim
// C1:  .  [B] [K] [K] [K] [K] [B] [S]   inside + handle top
// C2:  .  [B] [K] [K] [K] [K] [B]  .    inside + handle gap (C-shape)
// C3:  .  [B] [K] [K] [K] [K] [B] [S]   inside + handle bottom
// C4:  .  [B] [B] [B] [B] [B] [B]  .    bottom rim
// C5: [S] [S] [S] [S] [S] [S] [S] [S]   saucer

let private cupRows =
  let e = Empty
  let B = Filled Color.SandyBrown
  let K = Filled Color.Grey3
  let S = Filled Color.Silver

  [
    [ e; B; B; B; B; B; B; e ]
    [ e; B; K; K; K; K; B; S ]
    [ e; B; K; K; K; K; B; e ]
    [ e; B; K; K; K; K; B; S ]
    [ e; B; B; B; B; B; B; e ]
    [ S; S; S; S; S; S; S; S ]
  ]

let private steamFrames =
  let e = Empty
  let S = Filled Color.Silver

  [
    [
      [ e; e; S; e; e; S; e; e ]
      [ e; S; e; S; S; e; S; e ]
      [ e; e; S; e; e; S; e; e ]
    ]
    [
      [ e; S; e; S; S; e; S; e ]
      [ e; e; S; e; e; S; e; e ]
      [ e; S; e; S; S; e; S; e ]
    ]
  ]

let widget (model: Model) : IWidget =
  { new IWidget with
      member _.Render(context: RenderContext) =
        let renderCell cell =
          match cell with
          | Empty -> emptyBlock
          | Filled color -> styledBlock color

        match model.State with
        | Breaking frame ->
          let steamRows = steamFrames.[(frame / 2) % 2]
          let allRows = steamRows @ cupRows

          let cupLines = allRows |> List.map (fun row -> row |> List.map renderCell |> Text.line)

          let label1 = model.ActiveDriver |> Option.defaultValue "?"
          let label2 = nextInList model.ConnectedUsers model.ActiveDriver |> Option.defaultValue "?"

          let infoLines = [
            Text.line [ Text.span "" ]
            Text.line [ Text.span (sprintf "  BREAK  %s" (formatTime model.Remaining)) ]
            Text.line [ Text.span (sprintf "  %s → %s" label1 label2) ]
          ]

          context.Render(paragraph (cupLines @ infoLines), context.Viewport)

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

            match row with
            | 0 ->
              if inCar then styledBlock Color.Silver
              elif finish then styledBlock Color.White
              else emptyBlock
            | 1 ->
              if finish then
                styledBlock Color.White
              elif inCar then
                match col - carPos with
                | 0 -> styledBlock Color.Grey15
                | 1 -> styledBlock (driverColor |> Option.defaultValue Color.Silver)
                | _ -> styledBlock Color.Grey15
              elif filled then
                styledBlock filledColor
              else
                styledBlock Color.Grey23
            | 2 ->
              if finish then
                styledBlock Color.White
              elif inCar then
                match col - carPos with
                | 0
                | 2 -> styledBlock Color.Grey3
                | _ -> styledBlock Color.Grey
              elif filled then
                styledBlock filledColor
              else
                styledBlock Color.Grey23
            | _ -> emptyBlock

          let stateStr =
            match model.State with
            | Running -> "▶"
            | Paused -> "||"
            | Flashing _ -> "!!!"
            | _ -> "■"

          let driverLabel = model.ActiveDriver |> Option.defaultValue "(no driver)"

          let roadLines = [
            for row in 0..3 -> Text.line [ for col in 0 .. (roadWidth - 1) -> roadCell row col ]
          ]

          let infoLine =
            Text.line [
              Text.span (sprintf "  %s %s  %s" stateStr (formatTime model.Remaining) driverLabel)
            ]

          context.Render(paragraph (roadLines @ [ infoLine ]), context.Viewport)
  }
