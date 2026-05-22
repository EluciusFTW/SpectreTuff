module Session

open System.Collections.Generic

[<CLIMutable>]
type Data = {
  Goal: string
  StartedAt: int64
  ConnectedUsers: Dictionary<string, bool>
}
