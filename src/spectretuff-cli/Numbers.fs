module Logic

type Model = { Count: int }

type Msg =
  | Increment of int
  | Decrement

let update msg model =
  match msg with
  | Increment n -> { model with Count = model.Count + n }, []
  | Decrement -> { model with Count = model.Count - 1 }, []
