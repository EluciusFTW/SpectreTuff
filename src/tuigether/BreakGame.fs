module BreakGame

open System

type GamePhase =
  | WaitingForStart
  | Playing
  | GameOver of ticksLeft: int

type Obstacle = {
  X: float
  Width: int
  Height: int
  ColorTag: int
}

type GameModel = {
  Phase: GamePhase
  CarY: float
  CarVelocity: float
  CanDoubleJump: bool
  Obstacles: Obstacle list
  Score: int
  Speed: float
  TickCount: int
}

let private gravity = 0.9
let private jumpVelocity = 3.0
let private doubleJumpVelocity = 4.5
let private gameOverTicks = 18
let private initialSpeed = 0.9
let private maxSpeed = 2.5
let private carCol = 4

let init () = {
  Phase = WaitingForStart
  CarY = 0.0
  CarVelocity = 0.0
  CanDoubleJump = true
  Obstacles = []
  Score = 0
  Speed = 1.0
  TickCount = 0
}

let start (roadWidth: int) (model: GameModel) =
  let firstObstacle = {
    X = float (carCol + 20)
    Width = 1
    Height = 1
    ColorTag = 0
  }

  {
    model with
        Phase = Playing
        CarY = 0.0
        CarVelocity = 0.0
        CanDoubleJump = true
        Obstacles = [ firstObstacle ]
        Score = 0
        Speed = initialSpeed
        TickCount = 0
  }

let jump (model: GameModel) =
  match model.CarY with
  | y when y <= 0.0 -> {
      model with
          CarVelocity = jumpVelocity
    }
  | _ when model.CanDoubleJump -> {
      model with
          CarVelocity = doubleJumpVelocity
          CanDoubleJump = false
    }
  | _ -> model

let private spawnObstacle (roadWidth: int) (speed: float) (tickCount: int) (rng: Random) =
  let height = if tickCount < 30 then 1 else rng.Next(1, 3)
  let gap = max 12.0 (float (rng.Next(10, 18)) - speed * 0.5)

  {
    X = float roadWidth + gap
    Width = 1
    Height = height
    ColorTag = rng.Next(7)
  }

let private collidesWithCar (carY: float) (obstacle: Obstacle) =
  let ox = int (Math.Round(obstacle.X))
  ox + obstacle.Width > carCol && ox < carCol + 3 && carY < float obstacle.Height

let tick (roadWidth: int) (model: GameModel) =
  match model.Phase with
  | WaitingForStart -> model
  | GameOver n when n > 0 -> { model with Phase = GameOver(n - 1) }
  | GameOver _ -> { model with Phase = WaitingForStart }
  | Playing ->
    let rng = Random.Shared

    let newCarY = model.CarY + model.CarVelocity
    let newVelocity = model.CarVelocity - gravity
    let clampedY = max 0.0 newCarY

    let clampedVelocity =
      if clampedY <= 0.0 && newVelocity < 0.0 then
        0.0
      else
        newVelocity

    let movedObstacles = model.Obstacles |> List.map (fun o -> { o with X = o.X - model.Speed })

    let passed =
      List.zip model.Obstacles movedObstacles
      |> List.filter (fun (orig, moved) -> orig.X >= float carCol && moved.X < float carCol)
      |> List.length

    let filteredObstacles = movedObstacles |> List.filter (fun o -> o.X > float carCol - 2.0)

    let newObstacles =
      match filteredObstacles with
      | [] -> [ spawnObstacle roadWidth model.Speed model.TickCount rng ]
      | obs ->
        let last = obs |> List.maxBy (fun o -> o.X)

        if last.X < float (carCol + 8) then
          obs @ [ spawnObstacle roadWidth model.Speed model.TickCount rng ]
        else
          obs

    let collision = newObstacles |> List.exists (collidesWithCar clampedY)

    let newScore = model.Score + passed
    let newSpeed = min maxSpeed (initialSpeed + sqrt (float (model.TickCount + 1)) * 0.001)

    if collision then
      {
        model with
            Phase = GameOver gameOverTicks
            CarY = clampedY
            CarVelocity = 0.0
            Obstacles = newObstacles
      }
    else
      {
        model with
            CarY = clampedY
            CarVelocity = clampedVelocity
            CanDoubleJump = if clampedY <= 0.0 then true else model.CanDoubleJump
            Obstacles = newObstacles
            Score = newScore
            Speed = newSpeed
            TickCount = model.TickCount + 1
      }
