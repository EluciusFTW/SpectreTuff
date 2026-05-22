# Avatar Widget Design

**Date:** 2026-05-22

## Summary

Add an `Avatar` widget to `spectretuff` that renders pixel-art monster avatars using Unicode block characters. A fixed library of ~10 hand-crafted creatures is included. The CLI demo adds a panel with a creature list + live preview.

---

## Data Model

```fsharp
type Cell = Empty | Filled of Color

type Creature = {
    Name: string
    Rows: Cell list list  // 12 rows × 12 cells each
}
```

- Grid: **12×12 pixels**
- Rendering: each cell → `██` (2 chars) for square-ish pixels in terminal; `Empty` → `  ` (2 spaces)
- Terminal footprint per avatar: 24 chars wide × 12 rows

---

## Creature Library

~10 creatures defined as F# values in `Avatar.fs`, exposed as `Avatar.library : Creature list`.

Each creature hand-crafted using `Cell list list` literals. Colors drawn from `Spectre.Console.Color`.

---

## Widget API

Located at `src/spectretuff/Widgets/Avatar.fs`.

```fsharp
// Constructors
val avatar        : Creature -> AvatarWidget
val randomAvatar  : unit -> AvatarWidget
val avatarByIndex : int -> AvatarWidget        // wraps around library length

// Modifier
val withLook : Look -> AvatarWidget -> AvatarWidget

// Library access
val library : Creature list
```

Rendering: builds a Spectre `Rows` widget of 12 `Markup` lines. Each line is a sequence of `[color]██[/]` or `  ` segments.

---

## CLI Demo

Located at `src/spectretuff-cli/AvatarWidget.fs`.

### Layout

```
┌─ Avatar ─────────────────────────────┐
│ ┌─ Creatures ───┐  ┌─ Preview ─────┐ │
│ │ > Blobby      │  │   ████████   │ │
│ │   Spike       │  │  ██ oo   ██  │ │
│ │   Gloop       │  │  ██  \/  ██  │ │
│ │   Fangs       │  │   ████████   │ │
│ └───────────────┘  └──────────────┘ │
└──────────────────────────────────────┘
```

### Elmish

```fsharp
type Model = {
    ListModel: ListWidget.Model  // selection index drives avatar shown
}

type Msg = ListMsg of ListWidget.Msg
```

Keys: `↑`/`↓` navigate list (inherited from `ListWidget`). No extra key handling needed.

The selected list index maps directly to `Avatar.avatarByIndex`.

---

## Integration in Application.fs

- 4th panel added to the top horizontal split (alongside List, Timer, Counter)
- Focus key `4` added
- `AvatarModel` field added to main `Model`
- `AvatarMsg` case added to main `Msg`

---

## Files

| File | Change |
|------|--------|
| `src/spectretuff/Widgets/Avatar.fs` | New — data model, library, widget |
| `src/spectretuff/spectretuff.fsproj` | Add `Avatar.fs` |
| `src/spectretuff-cli/AvatarWidget.fs` | New — Elmish screen |
| `src/spectretuff-cli/spectretuff-cli.fsproj` | Add `AvatarWidget.fs` |
| `src/spectretuff-cli/Application.fs` | Add 4th panel + focus key 4 |

---

## Non-goals

- No procedural/algorithmic generation
- No seed-based deterministic avatars
- No animation
- No tuigether integration (separate concern)
