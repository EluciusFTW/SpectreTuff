open System
open Dependencies

match Config.load () with
| Error message ->
  eprintfn "%s" message
  exit 1
| Ok settings ->
  let client =
    Firebase.createClient {
      Url = settings.FirebaseUrl
      Secret = settings.FirebaseSecret
    }

  let terminal = Spectre.Tui.Terminal.Create()
  // Work around ConPTY alt-screen sizing bug: exit and re-enter alt-screen
  // so ConPTY allocates the buffer with the real window dimensions.
  Console.Write "\x1b[?1049l"
  Console.Out.Flush()
  System.Threading.Thread.Sleep 30
  Console.Write "\x1b[?1049h"
  Console.Out.Flush()

  let renderer = Spectre.Tui.Renderer terminal
  renderer.NoTargetFps()

  let notify =
    match settings.NotificationsEnabled with
    | true -> Notification.send
    | false -> fun _ -> ()

  let deps: Dependencies = { Client = client; Notify = notify }

  Elmish.Program.mkProgram
    (Application.init client settings.TuigetherUser)
    (Application.update deps settings.TuigetherUser)
    (Application.view renderer)
  |> Elmish.Program.withSubscription (fun model ->
    Input.subscription Application.InputMsg model
    @ Tick.subscription (TimeSpan.FromMilliseconds 200.0) Application.Tick model
    @ Application.subscriptions model)
  |> Elmish.Program.withTrace Application.traceToLog
  |> Elmish.Program.run

  Application.exitEvent.Wait()
  Console.Clear()
