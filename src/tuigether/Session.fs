module Session

open System.Collections.Generic

[<CLIMutable>]
type Data = {
  Goal: string
  StartedAt: int64
  Creator: string
  ConnectedUsers: Dictionary<string, bool>
}
