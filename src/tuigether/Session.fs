module Session

open System.Collections.Generic

[<CLIMutable>]
type UserPresence = { Avatar: string; Mood: string }

[<CLIMutable>]
type Data = {
  Goal: string
  StartedAt: int64
  Creator: string
  ConnectedUsers: Dictionary<string, UserPresence>
  ActiveDriver: string
}
