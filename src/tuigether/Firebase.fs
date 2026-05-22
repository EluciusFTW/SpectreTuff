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
  | WidgetStateChanged of string * Session.WidgetState option
  | ConnectedUserChanged of sessionId: string * user: string * presence: Session.UserPresence
  | ConnectedUserRemoved of sessionId: string * user: string

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
        Session.Data.ActiveDriver = null
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

let joinSession
  (client: FirebaseClient)
  (sessionId: string)
  (user: string)
  (avatarName: string)
  : Async<Result<unit, string>> =
  async {
    try
      let presence = {
        Session.UserPresence.Avatar = avatarName
        Session.UserPresence.Mood = "Neutral"
      }

      do!
        client
          .Child(sessionsPath)
          .Child(sessionId)
          .Child("widgetState")
          .Child("connectedUsers")
          .Child(user)
          .PutAsync(presence :> obj)
        |> Async.AwaitTask

      return Ok()
    with e ->
      return Error e.Message
  }

let leaveSession (client: FirebaseClient) (sessionId: string) (user: string) : Async<Result<unit, string>> =
  async {
    try
      do!
        client
          .Child(sessionsPath)
          .Child(sessionId)
          .Child("widgetState")
          .Child("connectedUsers")
          .Child(user)
          .DeleteAsync()
        |> Async.AwaitTask

      return Ok()
    with e ->
      return Error e.Message
  }

let setActiveDriver (client: FirebaseClient) sessionId (user: string) =
  async {
    do!
      client.Child(sessionsPath).Child(sessionId).Child("ActiveDriver").PutAsync(user :> obj)
      |> Async.AwaitTask
  }

let clearActiveDriver (client: FirebaseClient) sessionId =
  async {
    do!
      client.Child(sessionsPath).Child(sessionId).Child("ActiveDriver").DeleteAsync()
      |> Async.AwaitTask
  }

let setUserPresence (client: FirebaseClient) sessionId user (avatarName: string) (moodName: string) =
  async {
    let presence = {
      Session.UserPresence.Avatar = avatarName
      Session.UserPresence.Mood = moodName
    }

    do!
      client
        .Child(sessionsPath)
        .Child(sessionId)
        .Child("widgetState")
        .Child("connectedUsers")
        .Child(user)
        .PutAsync(presence :> obj)
      |> Async.AwaitTask
  }

let private subscribe (client: FirebaseClient) (dispatch: Msg -> unit) : IDisposable =
  // Initial snapshot — silently ignored if connection races; streaming catches up
  async {
    try
      let! sessions = client.Child(sessionsPath).OnceAsync<Session.Data>() |> Async.AwaitTask

      sessions
      |> Seq.map (fun o -> o.Key, o.Object)
      |> Seq.toList
      |> SessionsLoaded
      |> dispatch
    with _ ->
      ()
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
      dispatch (ConnectionError(sprintf "[%s] %s" (e.GetType().Name) e.Message))

  let onError (e: exn) =
    dispatch (ConnectionError(sprintf "[%s] %s" (e.GetType().Name) e.Message))

  client
    .Child(sessionsPath)
    .AsObservable<Session.Data>()
    .Subscribe(Action<FirebaseEvent<Session.Data>> onNext, Action<exn> onError)

let subscription (client: FirebaseClient) (wrap: Msg -> 'appMsg) _ = [
  [ "firebase" ], fun dispatch -> subscribe client (wrap >> dispatch)
]

let saveWidgetState (client: FirebaseClient) (sessionId: string) (state: Session.WidgetStateSave) : Async<unit> =
  async {
    try
      do!
        client.Child(sessionsPath).Child(sessionId).Child("widgetState").PatchAsync(state)
        |> Async.AwaitTask
    with _ ->
      ()
  }

let loadWidgetState (client: FirebaseClient) (sessionId: string) : Async<Session.WidgetState option> =
  async {
    try
      let! result =
        client.Child(sessionsPath).Child(sessionId).Child("widgetState").OnceSingleAsync<Session.WidgetState>()
        |> Async.AwaitTask

      return
        match isNull (box result) with
        | true -> None
        | false -> Some result
    with _ ->
      return None
  }

let private subscribeWidgetState (client: FirebaseClient) (sessionId: string) (dispatch: Msg -> unit) : IDisposable =
  let onNext (ev: FirebaseEvent<Session.WidgetState>) =
    try
      let state =
        match isNull (box ev.Object) with
        | true -> None
        | false -> Some ev.Object

      dispatch (WidgetStateChanged(sessionId, state))
    with e ->
      dispatch (ConnectionError e.Message)

  let onError (e: exn) =
    dispatch (ConnectionError e.Message)

  client
    .Child(sessionsPath)
    .Child(sessionId)
    .Child("widgetState")
    .AsObservable<Session.WidgetState>()
    .Subscribe(Action<FirebaseEvent<Session.WidgetState>> onNext, Action<exn> onError)

let widgetStateSubscription (client: FirebaseClient) (sessionId: string) (wrap: Msg -> 'appMsg) =
  [ "widget-state"; sessionId ], fun dispatch -> subscribeWidgetState client sessionId (wrap >> dispatch)

let private subscribeConnectedUsers (client: FirebaseClient) (sessionId: string) (dispatch: Msg -> unit) : IDisposable =
  let onNext (ev: FirebaseEvent<Session.UserPresence>) =
    try
      match String.IsNullOrEmpty ev.Key with
      | true -> ()
      | false ->
        match ev.EventType with
        | FirebaseEventType.Delete -> dispatch (ConnectedUserRemoved(sessionId, ev.Key))
        | _ ->
          match isNull (box ev.Object) with
          | true -> ()
          | false -> dispatch (ConnectedUserChanged(sessionId, ev.Key, ev.Object))
    with e ->
      dispatch (ConnectionError(sprintf "[%s] %s" (e.GetType().Name) e.Message))

  let onError (e: exn) =
    dispatch (ConnectionError(sprintf "[%s] %s" (e.GetType().Name) e.Message))

  client
    .Child(sessionsPath)
    .Child(sessionId)
    .Child("widgetState")
    .Child("connectedUsers")
    .AsObservable<Session.UserPresence>()
    .Subscribe(Action<FirebaseEvent<Session.UserPresence>> onNext, Action<exn> onError)

let connectedUsersSubscription (client: FirebaseClient) (sessionId: string) (wrap: Msg -> 'appMsg) =
  [ "connected-users"; sessionId ], fun dispatch -> subscribeConnectedUsers client sessionId (wrap >> dispatch)
