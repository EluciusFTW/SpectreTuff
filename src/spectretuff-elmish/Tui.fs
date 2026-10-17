namespace SpectreTuff.Elmish

open System
open System.Threading
open Elmish
open Spectre.Tui

module internal ExitSignal =
  // One TUI runs per process; `Tui.run` replaces the signal on every start.
  let mutable current = new ManualResetEventSlim false

module TuiCmd =
  /// Ends the application started with `Tui.run`.
  let exit<'msg> : Cmd<'msg> = Cmd.ofEffect (fun _ -> ExitSignal.current.Set())

module Tui =
  // Work around ConPTY alt-screen sizing bug: exit and re-enter alt-screen
  // so ConPTY allocates the buffer with the real window dimensions.
  let private reenterAltScreen () =
    Console.Write "\x1b[?1049l"
    Console.Out.Flush()
    Thread.Sleep 30
    Console.Write "\x1b[?1049h"
    Console.Out.Flush()

  /// Runs the program in the terminal and blocks until `TuiCmd.exit` is dispatched.
  /// The program's view returns the widget to draw; drawing is serialized, since
  /// subscriptions may dispatch from thread-pool threads and the terminal is not thread-safe.
  let run (program: Program<unit, 'model, 'msg, IWidget>) =
    let exitSignal = new ManualResetEventSlim false
    ExitSignal.current <- exitSignal

    let terminal = Terminal.Create()
    reenterAltScreen ()

    let renderer = Renderer terminal
    renderer.NoTargetFps()

    let view = Program.view program
    let renderLock = obj ()

    let draw model dispatch =
      lock renderLock (fun () -> renderer.Draw(fun ctx _ -> ctx.Render(view model dispatch)))

    program |> Program.withSetState draw |> Program.run

    exitSignal.Wait()
    terminal.Dispose()
    Console.Clear()
