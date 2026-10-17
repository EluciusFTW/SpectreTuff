module SpectreTuff.Elmish.Subscriptions

open System
open System.Threading
open Elmish

// Polls instead of blocking on Console.ReadKey: a blocked read can't be cancelled,
// so disposing the subscription would leave a thread waiting for one more key.
let private keyListener (dispatch: ConsoleKeyInfo -> unit) =
  let cts = new CancellationTokenSource()

  let rec loop () =
    async {
      match Console.KeyAvailable with
      | true -> dispatch (Console.ReadKey true)
      | false -> ()

      do! Async.Sleep 10
      do! loop ()
    }

  Async.Start(loop (), cts.Token)

  {
    new IDisposable with
      member _.Dispose() =
        cts.Cancel()
  }

/// Dispatches every key pressed in the terminal, wrapped into the app's message.
let keys (wrap: ConsoleKeyInfo -> 'msg) : Sub<'msg> = [ [ "keys" ], fun dispatch -> keyListener (wrap >> dispatch) ]

/// Dispatches `msg` every `interval`. The interval is part of the subscription id,
/// so ticks with different intervals can run side by side.
let tick (interval: TimeSpan) (msg: 'msg) : Sub<'msg> = [
  [ "tick"; string interval.TotalMilliseconds ],
  fun dispatch ->
    let timer = new Timer((fun _ -> dispatch msg), null, interval, interval)

    {
      new IDisposable with
        member _.Dispose() =
          timer.Dispose()
    }
]
