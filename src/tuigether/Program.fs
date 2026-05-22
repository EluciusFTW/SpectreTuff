open System

let url = Environment.GetEnvironmentVariable "FIREBASE_URL"
let secret = Environment.GetEnvironmentVariable "FIREBASE_SECRET"
let user = Environment.GetEnvironmentVariable "TUIGETHER_USER"

if
  String.IsNullOrWhiteSpace url
  || String.IsNullOrWhiteSpace secret
  || String.IsNullOrWhiteSpace user
then
  eprintfn "FIREBASE_URL, FIREBASE_SECRET, and TUIGETHER_USER environment variables are required."
  exit 1

let client = Firebase.createClient { Url = url; Secret = secret }

Console.Clear()
let terminal = Spectre.Tui.Terminal.Create()
let renderer = Spectre.Tui.Renderer terminal
renderer.NoTargetFps()

Elmish.Program.mkProgram (Application.init user) (Application.update client user) (Application.view renderer)
|> Elmish.Program.withSubscription (fun model ->
  Firebase.subscription client Application.FirebaseMsg model
  @ Input.subscription Application.InputMsg model)
|> Elmish.Program.withTrace Application.traceToLog
|> Elmish.Program.run

Application.exitEvent.Wait()
Console.Clear()
