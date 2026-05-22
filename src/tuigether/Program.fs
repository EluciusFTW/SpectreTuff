open System

let url = Environment.GetEnvironmentVariable "FIREBASE_URL"
let secret = Environment.GetEnvironmentVariable "FIREBASE_SECRET"

if String.IsNullOrWhiteSpace url || String.IsNullOrWhiteSpace secret then
  eprintfn "FIREBASE_URL and FIREBASE_SECRET environment variables are required."
  exit 1

let client = Firebase.createClient { Url = url; Secret = secret }

Console.Clear()
let terminal = Spectre.Tui.Terminal.Create()
let renderer = Spectre.Tui.Renderer terminal
renderer.NoTargetFps ()

Elmish.Program.mkProgram Application.init (Application.update client) (Application.view renderer)
|> Input.withKeyListener Application.InputMsg
|> Firebase.withFirebaseSubscription client Application.FirebaseMsg
|> Elmish.Program.withTrace Application.traceToLog
|> Elmish.Program.run

Application.exitEvent.Wait()
