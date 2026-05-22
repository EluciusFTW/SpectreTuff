module Tick

open System
open System.Threading

let subscription (interval: TimeSpan) (msg: 'appMsg) _ = [
  [ "tick" ],
  fun dispatch ->
    let timer = new Timer((fun _ -> dispatch msg), null, interval, interval)

    { new IDisposable with
        member _.Dispose() =
          timer.Dispose()
    }
]
