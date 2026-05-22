module Input

open System

type Msg = KeyPressed of ConsoleKeyInfo
type Model = unit

let keyListener =
  let sub dispatch =
    let cts = new Threading.CancellationTokenSource()

    let rec loop () =
      async {
        match Console.KeyAvailable with
        | true -> dispatch (KeyPressed(Console.ReadKey true))
        | false -> ()

        do! Async.Sleep 10
        do! loop ()
      }

    Async.Start(loop (), cts.Token)

    { new IDisposable with
        member _.Dispose() =
          cts.Cancel()
    }

  sub

let subscription (wrap: Msg -> 'appMsg) _ = [ [ "keys" ], fun dispatch -> keyListener (wrap >> dispatch) ]
