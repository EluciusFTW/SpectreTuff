open System

Console.Clear()
let terminal = Spectre.Tui.Terminal.Create()
let renderer = Spectre.Tui.Renderer terminal
renderer.SetTargetFps 60

Elmish.Program.mkProgram Application.init Application.update (Application.view renderer)
|> Input.withKeyListener Application.InputMsg
|> Elmish.Program.withTrace Application.traceToLog
|> Elmish.Program.run

Application.exitEvent.Wait()
