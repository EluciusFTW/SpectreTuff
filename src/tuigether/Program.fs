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

Elmish.Program.mkProgram (Application.init user) (Application.update client user) (Application.view renderer)
|> Elmish.Program.withSubscription (fun model ->
  Firebase.subscription client Application.FirebaseMsg model
  @ Input.subscription Application.InputMsg model
  @ Tick.subscription (TimeSpan.FromMilliseconds 200.0) Application.Tick model
  @ (match model.Page with
     | Application.SessionViewPage viewModel -> [
         Firebase.widgetStateSubscription client viewModel.SessionId Application.FirebaseMsg
         Firebase.connectedUsersSubscription client viewModel.SessionId Application.FirebaseMsg
       ]
     | Application.SessionListPage -> []))
|> Elmish.Program.withTrace Application.traceToLog
|> Elmish.Program.run

Application.exitEvent.Wait()
Console.Clear()
