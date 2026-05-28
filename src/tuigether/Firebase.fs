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

  let create
    (client: FirebaseClient)
    (user: string)
    (title: string)
    (gitBranch: string)
    : Async<Result<string, string>> =
    async {
      try
        let data = {
          Session.Data.Title = title
          Session.Data.Goal = ""
          Session.Data.StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
          Session.Data.WorkStartedAt = 0L
          Session.Data.Creator = user
          Session.Data.ActiveDriver = null
          Session.Data.Status = Session.Status.toString Session.Status.Created
          Session.Data.GoalLockOwner = null
          Session.Data.GoalLockedAt = 0L
          Session.Data.GitBranch = gitBranch
        }

        let! result = client.Child(sessionsPath).PostAsync(data) |> Async.AwaitTask
        return Ok result.Key
      with e ->
        return Error e.Message
    }

  let setStatus (client: FirebaseClient) (sessionId: string) (status: Session.Status) : Async<unit> =
    async {
      try
        do!
          client.Child(sessionsPath).Child(sessionId).Child("Status").PutAsync(Session.Status.toString status :> obj)
          |> Async.AwaitTask

        match status with
        | Session.Status.Started ->
          do!
            client
              .Child(sessionsPath)
              .Child(sessionId)
              .Child("WorkStartedAt")
              .PutAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() :> obj)
            |> Async.AwaitTask
        | _ -> ()
      with _ ->
        ()
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

  let saveGitBranch (client: FirebaseClient) (sessionId: string) (branch: string) : Async<unit> =
    async {
      try
        do!
          client.Child(sessionsPath).Child(sessionId).Child("GitBranch").PutAsync(branch :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let saveGoal (client: FirebaseClient) (sessionId: string) (text: string) : Async<unit> =
    async {
      try
        do!
          client.Child(sessionsPath).Child(sessionId).Child("Goal").PutAsync(text :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let saveGoalLock (client: FirebaseClient) (sessionId: string) (owner: string) (lockedAt: int64) : Async<unit> =
    async {
      try
        do!
          client.Child(sessionsPath).Child(sessionId).Child("GoalLockOwner").PutAsync(owner :> obj)
          |> Async.AwaitTask

        do!
          client.Child(sessionsPath).Child(sessionId).Child("GoalLockedAt").PutAsync(lockedAt :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let releaseGoalLock (client: FirebaseClient) (sessionId: string) : Async<unit> =
    async {
      try
        do!
          client.Child(sessionsPath).Child(sessionId).Child("GoalLockOwner").DeleteAsync()
          |> Async.AwaitTask

        do!
          client.Child(sessionsPath).Child(sessionId).Child("GoalLockedAt").DeleteAsync()
          |> Async.AwaitTask
      with _ ->
        ()
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

  // `subscriberTag` distinguishes subscribers of the same session's connectedUsers
  // path. Elmish keys subscriptions by their key list and keeps the first-registered
  // start function for a given key, so two consumers (e.g. SessionList and Journey)
  // sharing one key would silently route all events to whichever registered first.
  let subscription (client: FirebaseClient) (sessionId: string) (subscriberTag: string) (wrap: UserEvent -> 'appMsg) = [
    [ "connected-users"; subscriberTag; sessionId ],
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

  // Performs leave, then reads remaining connected users. The boolean indicates
  // whether the session is now empty — used by callers to drive the
  // Started → Finished transition without an extra roundtrip.
  let leaveAndCheckLast (client: FirebaseClient) (sessionId: string) (user: string) : Async<Result<bool, string>> =
    async {
      try
        do!
          (connectedUsersPath client sessionId).Child(user).DeleteAsync()
          |> Async.AwaitTask

        let! remaining =
          (connectedUsersPath client sessionId).OnceAsync<Session.UserPresence>()
          |> Async.AwaitTask

        return Ok(Seq.isEmpty remaining)
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

// ─── Push IDs ────────────────────────────────────────────────────────────────
//
// Port of Firebase's client-side push-ID algorithm: 20-character lexicographically
// sortable IDs (8 chars timestamp + 12 chars randomness). Concurrent generation
// across users yields distinct keys, so per-item writes never collide.

module PushId =

  let private chars = "-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"

  let private rng = Random()
  let private syncLock = obj ()
  let private lastRandChars: int array = Array.zeroCreate 12
  let mutable private lastPushTime = 0L

  let generate () =
    lock syncLock (fun () ->
      let timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
      let duplicateTime = timestamp = lastPushTime
      lastPushTime <- timestamp

      let timestampChars = Array.zeroCreate<char> 8
      let mutable remaining = timestamp

      for i in 7..-1..0 do
        timestampChars.[i] <- chars.[int (remaining % 64L)]
        remaining <- remaining / 64L

      match duplicateTime with
      | false ->
        for i in 0..11 do
          lastRandChars.[i] <- rng.Next(64)
      | true ->
        let mutable carryIndex = 11

        while carryIndex >= 0 && lastRandChars.[carryIndex] = 63 do
          lastRandChars.[carryIndex] <- 0
          carryIndex <- carryIndex - 1

        lastRandChars.[carryIndex] <- lastRandChars.[carryIndex] + 1

      let randChars = lastRandChars |> Array.map (fun n -> chars.[n])
      String(timestampChars) + String(randChars))

// ─── Notes ───────────────────────────────────────────────────────────────────

module Notes =

  let private notesPath (client: FirebaseClient) (sessionId: string) =
    client.Child(sessionsPath).Child(sessionId).Child("widgetState").Child("notes")

  let saveFreetext (client: FirebaseClient) (sessionId: string) (text: string) : Async<unit> =
    async {
      try
        do!
          (notesPath client sessionId).Child("FreetextContent").PutAsync(text :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let saveNoteMode (client: FirebaseClient) (sessionId: string) (mode: string) : Async<unit> =
    async {
      try
        do!
          (notesPath client sessionId).Child("NoteMode").PutAsync(mode :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let addItem (client: FirebaseClient) (sessionId: string) (itemId: string) (text: string) : Async<unit> =
    async {
      try
        do!
          (notesPath client sessionId).Child("ListItems").Child(itemId).PutAsync(text :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let deleteItem (client: FirebaseClient) (sessionId: string) (itemId: string) : Async<unit> =
    async {
      try
        do!
          (notesPath client sessionId).Child("ListItems").Child(itemId).DeleteAsync()
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let saveLock (client: FirebaseClient) (sessionId: string) (owner: string) (lockedAt: int64) : Async<unit> =
    async {
      try
        do!
          (notesPath client sessionId).Child("LockOwner").PutAsync(owner :> obj)
          |> Async.AwaitTask

        do!
          (notesPath client sessionId).Child("LockedAt").PutAsync(lockedAt :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let releaseLock (client: FirebaseClient) (sessionId: string) : Async<unit> =
    async {
      try
        do! (notesPath client sessionId).Child("LockOwner").DeleteAsync() |> Async.AwaitTask

        do! (notesPath client sessionId).Child("LockedAt").DeleteAsync() |> Async.AwaitTask
      with _ ->
        ()
    }

  let private loadField<'T> (client: FirebaseClient) (sessionId: string) (key: string) : Async<'T> =
    async {
      try
        let! result = (notesPath client sessionId).Child(key).OnceSingleAsync<'T>() |> Async.AwaitTask
        return result
      with _ ->
        return Unchecked.defaultof<'T>
    }

  // Load each field independently so a corrupted ListItems shape (e.g. legacy
  // sessions where Firebase coerced integer-keyed dicts into JSON arrays) does
  // not poison freetext and noteMode. ListItems just degrades to empty.
  let load (client: FirebaseClient) (sessionId: string) : Async<Session.NotesState option> =
    async {
      try
        let! freetext = loadField<string> client sessionId "FreetextContent"
        let! noteMode = loadField<string> client sessionId "NoteMode"

        let! listItems = loadField<System.Collections.Generic.Dictionary<string, string>> client sessionId "ListItems"

        let! lockOwner = loadField<string> client sessionId "LockOwner"
        let! lockedAt = loadField<int64> client sessionId "LockedAt"

        return
          Some {
            FreetextContent = freetext
            NoteMode = noteMode
            ListItems = listItems
            LockOwner = lockOwner
            LockedAt = lockedAt
          }
      with _ ->
        return None
    }

  let subscription (client: FirebaseClient) (sessionId: string) (wrap: Session.NotesState option -> 'appMsg) = [
    [ "notes-state"; sessionId ],
    fun dispatch ->
      subscribeReload (notesPath client sessionId) (fun () -> load client sessionId) (wrap >> dispatch) (fun _ -> ())
  ]

// ─── Todo ────────────────────────────────────────────────────────────────────

module Todo =

  let private todoPath (client: FirebaseClient) (sessionId: string) =
    client.Child(sessionsPath).Child(sessionId).Child("widgetState").Child("todo")

  let addItem (client: FirebaseClient) (sessionId: string) (itemId: string) (text: string) : Async<unit> =
    async {
      try
        do!
          (todoPath client sessionId).Child("Items").Child(itemId).Child("Text").PutAsync(text :> obj)
          |> Async.AwaitTask

        do!
          (todoPath client sessionId).Child("Items").Child(itemId).Child("Completed").PutAsync(false :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let setItem
    (client: FirebaseClient)
    (sessionId: string)
    (itemId: string)
    (text: string)
    (completed: bool)
    : Async<unit> =
    async {
      try
        do!
          (todoPath client sessionId).Child("Items").Child(itemId).Child("Text").PutAsync(text :> obj)
          |> Async.AwaitTask

        do!
          (todoPath client sessionId).Child("Items").Child(itemId).Child("Completed").PutAsync(completed :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let setCompleted (client: FirebaseClient) (sessionId: string) (itemId: string) (completed: bool) : Async<unit> =
    async {
      try
        do!
          (todoPath client sessionId).Child("Items").Child(itemId).Child("Completed").PutAsync(completed :> obj)
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let deleteItem (client: FirebaseClient) (sessionId: string) (itemId: string) : Async<unit> =
    async {
      try
        do!
          (todoPath client sessionId).Child("Items").Child(itemId).DeleteAsync()
          |> Async.AwaitTask
      with _ ->
        ()
    }

  let load (client: FirebaseClient) (sessionId: string) : Async<Session.TodoState option> =
    async {
      try
        let! items =
          (todoPath client sessionId)
            .Child("Items")
            .OnceSingleAsync<System.Collections.Generic.Dictionary<string, Session.TodoItemState>>()
          |> Async.AwaitTask

        return Some { Items = items }
      with _ ->
        return None
    }

  let subscription (client: FirebaseClient) (sessionId: string) (wrap: Session.TodoState option -> 'appMsg) = [
    [ "todo-state"; sessionId ],
    fun dispatch ->
      subscribeReload (todoPath client sessionId) (fun () -> load client sessionId) (wrap >> dispatch) (fun _ -> ())
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
