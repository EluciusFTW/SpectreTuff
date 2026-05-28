module Git

open System.Diagnostics

let readCurrentBranch () =
  try
    let psi = ProcessStartInfo("git", "branch --show-current")
    psi.UseShellExecute <- false
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    let proc = Process.Start(psi)
    let output = proc.StandardOutput.ReadToEnd().Trim()
    proc.WaitForExit()

    match proc.ExitCode = 0 && output <> "" with
    | true -> output
    | false -> ""
  with _ ->
    ""
