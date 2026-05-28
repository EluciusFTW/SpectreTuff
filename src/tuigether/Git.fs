module Git

open System.Diagnostics

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

let pull () : Async<Result<unit, string>> =
  async {
    return
      match runGit "pull" with
      | Ok _ -> Ok()
      | Error e -> Error e
  }
