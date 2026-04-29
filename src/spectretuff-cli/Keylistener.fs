module Input

open System
open Elmish

type Msg = KeyPressed of ConsoleKeyInfo
type Model = unit

let keyListener =
  let sub dispatch =
    let cts = new Threading.CancellationTokenSource()

    let rec loop () =
      async {
        let key = Console.ReadKey true
        dispatch (KeyPressed key)
        do! loop ()
      }

    Async.Start(loop (), cts.Token)

    { new IDisposable with
        member _.Dispose() =
          cts.Cancel()
    }

  sub

let withKeyListener (wrap: Msg -> 'appMsg) (program: Program<'arg, 'model, 'appMsg, 'view>) =
  program
  |> Program.withSubscription (fun _ -> [ [ "keys" ], fun dispatch -> keyListener (wrap >> dispatch) ])
