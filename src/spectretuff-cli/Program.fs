open SpectreTuff.Elmish

Elmish.Program.mkProgram Application.init Application.update Application.view
|> Elmish.Program.withSubscription (fun _ -> Subscriptions.keys Application.KeyPressed)
|> Elmish.Program.withTrace Application.traceToLog
|> Tui.run
