module Dependencies

open Firebase.Database

type Dependencies = {
  Client: FirebaseClient
  Notify: string -> unit
}
