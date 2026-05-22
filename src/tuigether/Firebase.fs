module Firebase

open System
open System.Threading.Tasks
open Firebase.Database
open Firebase.Database.Query
open Firebase.Database.Streaming

type Config = { Url: string; Secret: string }

type Msg =
  | SessionsLoaded of (string * Session.Data) list
  | SessionChanged of string * Session.Data
  | SessionRemoved of string
  | ConnectionError of string

let private sessionsPath = "sessions"

let createClient (cfg: Config) =
  let options = FirebaseOptions(AuthTokenAsyncFactory = Func<Task<string>>(fun () -> Task.FromResult cfg.Secret))

  new FirebaseClient(cfg.Url, options)

let private adjectives = [|
  "Wobbly"
  "Grumpy"
  "Suspicious"
  "Caffeinated"
  "Haunted"
  "Slightly Damp"
  "Overly Formal"
  "Chaotic"
  "Smugly Confident"
  "Reluctant"
  "Aggressively Mediocre"
  "Mildly Panicked"
  "Existentially Confused"
  "Eerily Calm"
  "Suspiciously Cheerful"
|]

let private nouns = [|
  "Penguin"
  "Rubber Duck"
  "Spreadsheet"
  "Turnip"
  "Wizard"
  "Bureaucrat"
  "Raccoon"
  "Algorithm"
  "Fondue Pot"
  "Yak"
  "Sock Puppet"
  "Time Machine"
  "Sandwich"
  "Narwhal"
  "Kumquat"
|]

let private randomSessionName () =
  let rng = Random()
  let adj = adjectives.[rng.Next adjectives.Length]
  let noun = nouns.[rng.Next nouns.Length]
  sprintf "The %s %s" adj noun

let createSession (client: FirebaseClient) (user: string) : Async<Result<string, string>> =
  async {
    try
      let data = {
        Session.Data.Goal = randomSessionName ()
        Session.Data.StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        Session.Data.Creator = user
        Session.Data.ConnectedUsers = null
      }

      let! result = client.Child(sessionsPath).PostAsync(data) |> Async.AwaitTask
      return Ok result.Key
    with e ->
      return Error e.Message
  }

let deleteSession (client: FirebaseClient) (sessionId: string) : Async<Result<unit, string>> =
  async {
    try
      do! client.Child(sessionsPath).Child(sessionId).DeleteAsync() |> Async.AwaitTask
      return Ok()
    with e ->
      return Error e.Message
  }

let joinSession (client: FirebaseClient) (sessionId: string) (user: string) : Async<Result<unit, string>> =
  async {
    try
      do!
        client.Child(sessionsPath).Child(sessionId).Child("connectedUsers").Child(user).PutAsync(true)
        |> Async.AwaitTask

      return Ok()
    with e ->
      return Error e.Message
  }

let leaveSession (client: FirebaseClient) (sessionId: string) (user: string) : Async<Result<unit, string>> =
  async {
    try
      do!
        client.Child(sessionsPath).Child(sessionId).Child("connectedUsers").Child(user).DeleteAsync()
        |> Async.AwaitTask

      return Ok()
    with e ->
      return Error e.Message
  }

let private subscribe (client: FirebaseClient) (dispatch: Msg -> unit) : IDisposable =
  async {
    try
      let! sessions = client.Child(sessionsPath).OnceAsync<Session.Data>() |> Async.AwaitTask

      sessions
      |> Seq.map (fun o -> o.Key, o.Object)
      |> Seq.toList
      |> SessionsLoaded
      |> dispatch
    with e ->
      dispatch (ConnectionError(sprintf "initial read failed: %s" e.Message))
  }
  |> Async.Start

  let onNext (ev: FirebaseEvent<Session.Data>) =
    try
      match String.IsNullOrEmpty ev.Key, isNull (box ev.Object) with
      | false, false ->
        match ev.EventType with
        | FirebaseEventType.Delete -> dispatch (SessionRemoved ev.Key)
        | _ -> dispatch (SessionChanged(ev.Key, ev.Object))
      | _ -> ()
    with e ->
      dispatch (ConnectionError e.Message)

  let onError (e: exn) =
    dispatch (ConnectionError e.Message)

  client
    .Child(sessionsPath)
    .AsObservable<Session.Data>()
    .Subscribe(Action<FirebaseEvent<Session.Data>> onNext, Action<exn> onError)

let subscription (client: FirebaseClient) (wrap: Msg -> 'appMsg) _ = [
  [ "firebase" ], fun dispatch -> subscribe client (wrap >> dispatch)
]
