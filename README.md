# SpectreTuff
_Spectre Tui for F#_ - A thin, opinionated wrapper around [Spectre.Tui](https://github.com/spectreconsole/spectre.tui)

> [!WARNING]
> This library, as well as Spectre.Tui is currently under construction and may change at any time.  

## Key bindings
`SpectreTuff.Keymap` lets one list of bindings drive both input handling and the help bar, so a key and its description can't drift apart:

```fsharp
open SpectreTuff.Keymap

let bindings = [
  KeyBinding.createSpecial ConsoleKey.UpArrow "up" MoveUp |> KeyBinding.orKey (CharKey 'k')
  KeyBinding.dynamic (CharKey 's') (fun model -> {
    Description = "save"
    Message = if model.Dirty then Some Save else None // None: disabled and hidden from help
  })
]

KeyBinding.handleKey bindings key model // ConsoleKeyInfo -> 'Msg option
KeyBinding.toKeyMap bindings model      // IKeyMap for the help widget
```

## Elmish
The `EluciusFTW.SpectreTuff.Elmish` package runs an [Elmish](https://github.com/elmish/elmish) program on SpectreTuff. It sets up the terminal, serializes rendering and handles exiting:

```fsharp
open SpectreTuff.Elmish

// view: 'model -> Dispatch<'msg> -> IWidget
// update: | Quit -> model, TuiCmd.exit
Program.mkProgram init update view
|> Program.withSubscription (fun model ->
  Subscriptions.keys KeyPressed // KeyPressed of ConsoleKeyInfo
  @ Subscriptions.tick (TimeSpan.FromSeconds 1.0) Tick)
|> Tui.run // blocks until TuiCmd.exit
```

To forward a child component's subscriptions to its parent, use Elmish's `Sub.map "child" ChildMsg childSubs`.

## Versioning
We are using [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) and follow the version scheme: `<major>.<minor>.<git-depth>` for out releases.

Since this package is a wrapper around _Spectre.Tui_, we will synchronize our major and minor versions with the ones of the Spectre dependency we are wrapping.

> <b>Note</b>: In particular, the _third number_ in the version does not have the same meaning as the patches in SemVer. Increments in that number may contain breaking changes, in contrast to patch versions in SemVer.

## License
Copyright © Guy Buss, Daniel Muckelbauer

SpectreTuff is provided as-is under the MIT license.
See the LICENSE.md file included in the repository.
