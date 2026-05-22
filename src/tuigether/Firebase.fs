module Firebase

open System
open System.Threading.Tasks
open Elmish
open Firebase.Database
open Firebase.Database.Query
open Firebase.Database.Streaming

type Config = { Url: string; Secret: string }

type Msg =
  | CountUpdated of int
  | ConnectionError of string

let private counterPath = "counter"

let createClient (cfg: Config) =
  let options =
    FirebaseOptions(AuthTokenAsyncFactory = Func<Task<string>>(fun () -> Task.FromResult cfg.Secret))
  new FirebaseClient(cfg.Url, options)

let private subscribe (client: FirebaseClient) (dispatch: Msg -> unit) : IDisposable =
  // Initial one-shot read: verifies auth and seeds counter node if missing.
  // This also fires CountUpdated immediately so status flips from "connecting…".
  async {
    try
      let! current = client.Child(counterPath).OnceSingleAsync<int>() |> Async.AwaitTask
      dispatch (CountUpdated current)
    with e -> dispatch (ConnectionError (sprintf "initial read failed: %s" e.Message))
  }
  |> Async.Start

  let onNext (ev: FirebaseEvent<int>) =
    try dispatch (CountUpdated ev.Object)
    with e -> dispatch (ConnectionError e.Message)
  let onError (e: exn) = dispatch (ConnectionError e.Message)
  client
    .Child(counterPath)
    .AsObservable<int>()
    .Subscribe(Action<FirebaseEvent<int>> onNext, Action<exn> onError)

let increment (client: FirebaseClient) : Async<Result<unit, string>> =
  async {
    try
      let! current = client.Child(counterPath).OnceSingleAsync<int>() |> Async.AwaitTask
      do! client.Child(counterPath).PutAsync(current + 1) |> Async.AwaitTask
      return Ok ()
    with e -> return Error e.Message
  }

let subscription (client: FirebaseClient) (wrap: Msg -> 'appMsg) _ =
  [ [ "firebase" ], fun dispatch -> subscribe client (wrap >> dispatch) ]
