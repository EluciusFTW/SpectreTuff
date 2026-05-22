# SpectreTuff

Functional F# wrapper around [Spectre.Tui](https://github.com/spectreconsole/spectre.tui).

## Projects

- `src/spectretuff` — the library (F# wrapper API, no executable)
- `src/spectretuff-cli` — CLI demo app using Elmish
- `src/tuigether` — experimental multi-user TUI app using Elmish + Firebase

## Build & run

```bash
dotnet build
dotnet run --project src/spectretuff-cli
```

## Formatting

Fantomas is configured as a local dotnet tool. After generating or editing any F# code, always run:

```bash
dotnet fantomas <file>
```

Formatting rules are in `.editorconfig`.

## Conventions

- No abbreviations in parameter names.
- Boolean toggles use paired functions (`showFoo`/`hideFoo`, `foo`/`noFoo`) — never `withFoo: bool`.
- API names are symmetric (e.g. `withHorizontalAlignment` / `withVerticalAlignment`).
- Prefer `match` over `if`/`else`.
