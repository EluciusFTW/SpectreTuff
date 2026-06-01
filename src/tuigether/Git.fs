module Git

open System
open System.Diagnostics

let private runGitArgs (args: string list) : Result<string, string> =
  try
    let psi = ProcessStartInfo("git")

    for a in args do
      psi.ArgumentList.Add(a)

    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    let proc = Process.Start(psi)
    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    match proc.ExitCode = 0 with
    | true -> Ok(stdout.Trim())
    | false ->
      let combined = (stderr + stdout).Trim()

      match combined with
      | "" -> Error(sprintf "git exited with code %d" proc.ExitCode)
      | text -> Error text
  with ex ->
    Error ex.Message

let private runGit (args: string) : Result<string, string> =
  try
    let psi = ProcessStartInfo("git", args)
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    let proc = Process.Start(psi)
    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    match proc.ExitCode = 0 with
    | true -> Ok(stdout.Trim())
    | false ->
      let combined = (stderr + stdout).Trim()

      match combined with
      | "" -> Error(sprintf "git exited with code %d" proc.ExitCode)
      | text -> Error text
  with ex ->
    Error ex.Message

let readCurrentBranch () =
  match runGit "branch --show-current" with
  | Ok branch when branch <> "" -> branch
  | _ -> ""

let private stripGitSuffix (text: string) =
  match text.EndsWith(".git") with
  | true -> text.Substring(0, text.Length - 4)
  | false -> text

let private lastPathSegment (text: string) =
  match text.LastIndexOfAny([| '/'; ':' |]) with
  | -1 -> text
  | i -> text.Substring(i + 1)

let readRepoName () =
  let fromRemote =
    match runGit "config --get remote.origin.url" with
    | Ok url when url <> "" -> url.TrimEnd('/') |> stripGitSuffix |> lastPathSegment
    | _ -> ""

  match fromRemote with
  | "" ->
    match runGit "rev-parse --show-toplevel" with
    | Ok path when path <> "" -> System.IO.Path.GetFileName(path.TrimEnd('/'))
    | _ -> ""
  | name -> name

let createAndPushBranch (name: string) : Async<Result<unit, string>> =
  async {
    return
      match runGit (sprintf "checkout -b %s" name) with
      | Error e -> Error e
      | Ok _ ->
        match runGit (sprintf "push -u origin %s" name) with
        | Ok _ -> Ok()
        | Error e -> Error e
  }

let fetchAndCheckout (name: string) : Async<Result<unit, string>> =
  async {
    return
      match runGit (sprintf "fetch origin %s" name) with
      | Error e -> Error e
      | Ok _ ->
        match runGit (sprintf "checkout %s" name) with
        | Ok _ -> Ok()
        | Error e -> Error e
  }

let private aheadBehind () : Result<int * int, string> =
  match runGit "rev-list --left-right --count @{upstream}...HEAD" with
  | Error e -> Error e
  | Ok output ->
    let parts = output.Split([| '\t'; ' ' |], StringSplitOptions.RemoveEmptyEntries)

    match parts.Length, parts with
    | 2, [| behindText; aheadText |] ->
      match Int32.TryParse behindText, Int32.TryParse aheadText with
      | (true, behind), (true, ahead) -> Ok(ahead, behind)
      | _ -> Error(sprintf "unexpected rev-list output: %s" output)
    | _ -> Error(sprintf "unexpected rev-list output: %s" output)

let private isDirty () =
  match runGit "status --porcelain" with
  | Ok output -> output <> ""
  | Error _ -> false

let wipSync (title: string) : Async<Result<unit, string>> =
  async {
    return
      match runGit "fetch" with
      | Error e -> Error e
      | Ok _ ->
        match aheadBehind () with
        | Error e -> Error e
        | Ok(_, behind) when behind > 0 -> Error "behind origin — sync first"
        | Ok(ahead, _) ->
          let dirty = isDirty ()

          let commitStep =
            match dirty with
            | false -> Ok ""
            | true ->
              match runGit "add -A" with
              | Error e -> Error e
              | Ok _ -> runGitArgs [ "commit"; "-m"; sprintf "WIP: %s" title ]

          match commitStep with
          | Error e -> Error e
          | Ok _ ->
            match not dirty && ahead = 0 with
            | true -> Error "nothing to sync"
            | false ->
              match runGit "push" with
              | Ok _ -> Ok()
              | Error e -> Error e
  }

type SyncResult =
  | Synced
  // No ff path — origin rebased/amended. Counts for the prompt.
  | Diverged of ahead: int * behind: int

let syncCurrentBranch () : Async<Result<SyncResult, string>> =
  async {
    return
      match runGit "fetch" with
      | Error e -> Error e
      | Ok _ ->
        match aheadBehind () with
        | Error e -> Error e
        | Ok(ahead, behind) ->
          match ahead, behind with
          | _, 0 when ahead > 0 ->
            match runGit "push" with
            | Ok _ -> Ok Synced
            | Error e -> Error e
          | 0, _ when behind > 0 ->
            // --ff-only: no merge commit. Non-ff fails clean, tree untouched.
            match runGit "pull --ff-only" with
            | Ok _ -> Ok Synced
            | Error e -> Error e
          | 0, 0 -> Ok Synced
          | _ -> Ok(Diverged(ahead, behind))
  }

// Destructive: drops local commits + working changes. Confirm first.
let resetToUpstream () : Async<Result<unit, string>> =
  async {
    return
      match runGit "fetch" with
      | Error e -> Error e
      | Ok _ ->
        match runGit "reset --hard @{upstream}" with
        | Ok _ -> Ok()
        | Error e -> Error e
  }
