module Firebase

open System
open System.Threading.Tasks
open Firebase.Database
open Firebase.Database.Query
open Firebase.Database.Streaming

type Config = { Url: string; Secret: string }

// Events emitted by the global sessions-list stream (used by SessionList).
type SessionEvent =
  | SessionsLoaded of (string * Session.Data) list
  | SessionChanged of string * Session.Data
  | SessionRemoved of string
  | ConnectionError of string

// Events emitted by the per-session connectedUsers stream (used by Avatar).
type UserEvent =
  | UserChanged of user: string * presence: Session.UserPresence
  | UserRemoved of user: string

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

let private formatError (e: exn) =
  sprintf "[%s] %s" (e.GetType().Name) e.Message

// ─── Generic reload-on-change subscription ───────────────────────────────────
//
// A path's children may be heterogeneous (scalars + dictionaries), so
// AsObservable<T> cannot deserialize per-child events into a full T. Treat
// any child event as a "something changed" signal and re-load the full
// payload via the supplied `load` callback. An explicit initial load runs
// once on registration so callers see current state without waiting for a
// remote change.
let private subscribeReload<'T>
  (pathQuery: ChildQuery)
  (load: unit -> Async<'T option>)
  (dispatch: 'T option -> unit)
  (onErrorMsg: string -> unit)
  : IDisposable =
  // Coalesce reload requests. The Firebase observable fires once per existing
  // child on subscribe, so naive Async.Start per event would dispatch the same
  // state N times in parallel. Keep one load in flight; if requests arrive
  // while loading, run exactly one follow-up after it.
  let gate = obj ()
  let mutable inFlight = false
  let mutable pending = false

  let rec runLoad () =
    async {
      try
        let! state = load ()
        dispatch state
      with e ->
        onErrorMsg (formatError e)

      let runAgain =
        lock gate (fun () ->
          match pending with
          | true ->
            pending <- false
            true
          | false ->
            inFlight <- false
            false)

      match runAgain with
      | true -> return! runLoad ()
      | false -> ()
    }

  let triggerReload () =
    let shouldStart =
      lock gate (fun () ->
        match inFlight with
        | true ->
          pending <- true
          false
        | false ->
          inFlight <- true
          true)

    match shouldStart with
    | true -> Async.Start(runLoad ())
    | false -> ()

  triggerReload ()

  let onNext (_ev: FirebaseEvent<obj>) =
    triggerReload ()

  let onError (e: exn) =
    onErrorMsg (formatError e)

  pathQuery.AsObservable<obj>().Subscribe(Action<FirebaseEvent<obj>> onNext, Action<exn> onError)

// ─── Sessions stream ─────────────────────────────────────────────────────────

module Sessions =

  let private subscribeSessions (client: FirebaseClient) (dispatch: SessionEvent -> unit) : IDisposable =
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
        dispatch (ConnectionError(formatError e))

    let onError (e: exn) =
      dispatch (ConnectionError(formatError e))

    client
      .Child(sessionsPath)
      .AsObservable<Session.Data>()
      .Subscribe(Action<FirebaseEvent<Session.Data>> onNext, Action<exn> onError)

  let subscription (client: FirebaseClient) (wrap: SessionEvent -> 'appMsg) _ = [
    [ "firebase-sessions" ], fun dispatch -> subscribeSessions client (wrap >> dispatch)
  ]

  let create (client: FirebaseClient) (user: string) : Async<Result<string, string>> =
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

  let delete (client: FirebaseClient) (sessionId: string) : Async<Result<unit, string>> =
    async {
      try
        do! client.Child(sessionsPath).Child(sessionId).DeleteAsync() |> Async.AwaitTask
        return Ok()
      with e ->
        return Error e.Message
    }

  let setActiveDriver (client: FirebaseClient) (sessionId: string) (user: string option) : Async<unit> =
    async {
      match user with
      | Some u ->
        do!
          client.Child(sessionsPath).Child(sessionId).Child("ActiveDriver").PutAsync(u :> obj)
          |> Async.AwaitTask
      | None ->
        do!
          client.Child(sessionsPath).Child(sessionId).Child("ActiveDriver").DeleteAsync()
          |> Async.AwaitTask
    }

  let private sessionDataPath (client: FirebaseClient) (sessionId: string) =
    client.Child(sessionsPath).Child(sessionId)

  let loadData (client: FirebaseClient) (sessionId: string) : Async<Session.Data option> =
    async {
      try
        let! result =
          (sessionDataPath client sessionId).OnceSingleAsync<Session.Data>()
          |> Async.AwaitTask

        return
          match isNull (box result) with
          | true -> None
          | false -> Some result
      with _ ->
        return None
    }

  let dataSubscription (client: FirebaseClient) (sessionId: string) (wrap: Session.Data option -> 'appMsg) = [
    [ "session-data"; sessionId ],
    fun dispatch ->
      subscribeReload
        (sessionDataPath client sessionId)
        (fun () -> loadData client sessionId)
        (wrap >> dispatch)
        (fun _ -> ())
  ]

// ─── Connected users (Avatar) ────────────────────────────────────────────────

module Users =

  let private connectedUsersPath (client: FirebaseClient) (sessionId: string) =
    client.Child(sessionsPath).Child(sessionId).Child("widgetState").Child("connectedUsers")

  let private subscribeConnectedUsers
    (client: FirebaseClient)
    (sessionId: string)
    (dispatch: UserEvent -> unit)
    (onConnectionError: string -> unit)
    : IDisposable =
    let onNext (ev: FirebaseEvent<Session.UserPresence>) =
      try
        match String.IsNullOrEmpty ev.Key with
        | true -> ()
        | false ->
          match ev.EventType with
          | FirebaseEventType.Delete -> dispatch (UserRemoved ev.Key)
          | _ ->
            match isNull (box ev.Object) with
            | true -> ()
            | false -> dispatch (UserChanged(ev.Key, ev.Object))
      with e ->
        onConnectionError (formatError e)

    let onError (e: exn) =
      onConnectionError (formatError e)

    (connectedUsersPath client sessionId)
      .AsObservable<Session.UserPresence>()
      .Subscribe(Action<FirebaseEvent<Session.UserPresence>> onNext, Action<exn> onError)

  let subscription (client: FirebaseClient) (sessionId: string) (wrap: UserEvent -> 'appMsg) = [
    [ "connected-users"; sessionId ],
    fun dispatch -> subscribeConnectedUsers client sessionId (wrap >> dispatch) (fun _ -> ())
  ]

  let join
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
          (connectedUsersPath client sessionId).Child(user).PutAsync(presence :> obj)
          |> Async.AwaitTask

        return Ok()
      with e ->
        return Error e.Message
    }

  let leave (client: FirebaseClient) (sessionId: string) (user: string) : Async<Result<unit, string>> =
    async {
      try
        do!
          (connectedUsersPath client sessionId).Child(user).DeleteAsync()
          |> Async.AwaitTask

        return Ok()
      with e ->
        return Error e.Message
    }

  let setPresence
    (client: FirebaseClient)
    (sessionId: string)
    (user: string)
    (avatarName: string)
    (moodName: string)
    : Async<unit> =
    async {
      let presence = {
        Session.UserPresence.Avatar = avatarName
        Session.UserPresence.Mood = moodName
      }

      do!
        (connectedUsersPath client sessionId).Child(user).PutAsync(presence :> obj)
        |> Async.AwaitTask
    }

// ─── Notes ───────────────────────────────────────────────────────────────────

module Notes =

  let private notesPath (client: FirebaseClient) (sessionId: string) =
    client.Child(sessionsPath).Child(sessionId).Child("widgetState").Child("notes")

  let save (client: FirebaseClient) (sessionId: string) (state: Session.NotesState) : Async<unit> =
    async {
      try
        do! (notesPath client sessionId).PutAsync(state :> obj) |> Async.AwaitTask
      with _ ->
        ()
    }

  let load (client: FirebaseClient) (sessionId: string) : Async<Session.NotesState option> =
    async {
      try
        let! result =
          (notesPath client sessionId).OnceSingleAsync<Session.NotesState>()
          |> Async.AwaitTask

        return
          match isNull (box result) with
          | true -> None
          | false -> Some result
      with _ ->
        return None
    }

  let subscription (client: FirebaseClient) (sessionId: string) (wrap: Session.NotesState option -> 'appMsg) = [
    [ "notes-state"; sessionId ],
    fun dispatch ->
      subscribeReload (notesPath client sessionId) (fun () -> load client sessionId) (wrap >> dispatch) (fun _ -> ())
  ]

// ─── Timer ───────────────────────────────────────────────────────────────────

module Timer =

  let private timerPath (client: FirebaseClient) (sessionId: string) =
    client.Child(sessionsPath).Child(sessionId).Child("widgetState").Child("timer")

  let save (client: FirebaseClient) (sessionId: string) (state: Session.TimerState) : Async<unit> =
    async {
      try
        do! (timerPath client sessionId).PutAsync(state :> obj) |> Async.AwaitTask
      with _ ->
        ()
    }

  let load (client: FirebaseClient) (sessionId: string) : Async<Session.TimerState option> =
    async {
      try
        let! result =
          (timerPath client sessionId).OnceSingleAsync<Session.TimerState>()
          |> Async.AwaitTask

        return
          match isNull (box result) with
          | true -> None
          | false -> Some result
      with _ ->
        return None
    }

  let subscription (client: FirebaseClient) (sessionId: string) (wrap: Session.TimerState option -> 'appMsg) = [
    [ "timer-state"; sessionId ],
    fun dispatch ->
      subscribeReload (timerPath client sessionId) (fun () -> load client sessionId) (wrap >> dispatch) (fun _ -> ())
  ]
