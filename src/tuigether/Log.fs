module Log

open System
open System.IO
open Spectre.Console
open Spectre.Tui
open SpectreTuff
open SpectreTuff.Widgets

type Model = { Entries: ResizeArray<string> }

let private fileLock = obj ()

let private logDir =
  match Environment.GetEnvironmentVariable "TUIGETHER_LOG_DIR" with
  | null
  | "" -> "logs"
  | dir -> dir

let private retentionDays =
  match Environment.GetEnvironmentVariable "TUIGETHER_LOG_RETENTION_DAYS" with
  | null
  | "" -> 14
  | s ->
    match Int32.TryParse s with
    | true, n when n >= 0 -> n
    | _ -> 14

let private currentLogPath () =
  let date = DateTime.Now.ToString "yyyy-MM-dd"
  Path.Combine(logDir, sprintf "tuigether-%s.log" date)

let private writeLine (entry: string) =
  try
    let ts = DateTime.Now.ToString "HH:mm:ss.fff"
    let line = sprintf "%s %s%s" ts entry Environment.NewLine

    use stream = new FileStream(currentLogPath (), FileMode.Append, FileAccess.Write, FileShare.ReadWrite)

    use writer = new StreamWriter(stream)
    writer.Write line
  with _ ->
    ()

let private cleanupOldLogs () =
  try
    let cutoff = DateTime.Now.Date.AddDays(float -retentionDays)
    let prefix = "tuigether-"

    for file in Directory.EnumerateFiles(logDir, "tuigether-*.log") do
      let datePart = (Path.GetFileNameWithoutExtension file).Substring prefix.Length

      match
        DateTime.TryParseExact(
          datePart,
          "yyyy-MM-dd",
          Globalization.CultureInfo.InvariantCulture,
          Globalization.DateTimeStyles.None
        )
      with
      | true, fileDate when fileDate.Date < cutoff -> File.Delete file
      | _ -> ()
  with _ ->
    ()

let init () =
  try
    Directory.CreateDirectory logDir |> ignore
    cleanupOldLogs ()
  with _ ->
    ()

  { Entries = ResizeArray<string>() }

let append (entry: string) (model: Model) =
  lock fileLock (fun () ->
    model.Entries.Add entry
    writeLine entry)

// File-only log line for code with no Log.Model in scope.
let line (entry: string) =
  lock fileLock (fun () -> writeLine entry)

// Cap how many trailing entries are rendered per frame. The panel auto-scrolls
// to the bottom, so older lines are off-screen anyway. Rendering the whole log
// every frame is O(entries) and, with the uncapped framerate, pegs the CPU and
// churns memory once the log gets long — freezing the app on open.
let private maxRenderedEntries = 500

let view (model: Model) (ctx: RenderContext) (area: Rectangle) =
  let entries =
    match model.Entries.Count with
    | count when count > maxRenderedEntries -> model.Entries.GetRange(count - maxRenderedEntries, maxRenderedEntries)
    | _ -> model.Entries

  let content = entries |> Seq.map (fun entry -> TextLine(TextSpan entry)) |> paragraph
  let logView = scrollView content
  logView.ScrollToBottom()
  ctx.Render(box (Look.fromColor Color.Grey) |> withTitle "Log" |> withInnerWidget logView, area)
