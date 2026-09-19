# MediaArchive

A personal, locally-run **media OS**: one place that tracks everything I've consumed
across every media type (books, games, films, shows) and surfaces taste insights.
Letterboxd + Goodreads + IGDB combined, but private, local, and unified.

This is **one application**: a .NET app that owns the database and business logic,
with a Swift framework on top that is the whole user interface. There is no
separate backend/frontend over a network, no API over HTTP, and no authentication:
it runs as a single local user, on my phone.

---

## Stack

| Concern | Choice |
|---|---|
| Host | **.NET MAUI** (iOS) — DI, lifecycle, the app bundle; no MAUI UI beyond one empty page |
| UI | **SwiftUI**, in `native/` — compiled by `xcodebuild` into `MediaArchiveUI.framework` and embedded |
| Runtime | .NET 10 |
| Data access | EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`) |
| Database | **SQLite** — `mediaarchive.db` in the app's container on the phone, migrated on launch |
| Auth | None (single local user) |

## Running it

There is nothing to start by hand — `MediaArchive.csproj` is a class library, and
everything runs through `./ma`:

```bash
./ma              # generate contracts, build the framework + widget + app, launch on the simulator
./ma phone        # renew signing, then the same onto the iPhone
./ma pull         # copy the phone's DB + covers into backups/ and refresh the repo DB
./ma --help       # everything else
```

Signing note: a free Apple ID only issues **7-day** provisioning profiles, so device
builds break every week. `./ma renew` reissues one non-interactively; `./ma phone`
does it automatically. The app refusing to open is the signal that the week is up.

```bash
dotnet ef migrations add <Name>   # after changing Models/ or DbContext
dotnet build                      # compile check of the library
scripts/sync-contracts.sh         # regenerate the Swift contracts (./ma does this)
scripts/sync-colors.sh            # push colors.json into the two Swift palettes
```

---

## How a page gets its data

Swift never touches the database. The service layer is shaped like a small API and
the Swift side is its client, in the same process:

```
SwiftUI view                                  native/Sources/<Surface>/
      │  reads state from
      ▼
HomeStore (one @Observable class per page)    fetch · decode · loading/error state
      │  api.home()
      ▼
Api (generated)  ──▶  Backend                 native/Sources/Generated, Bridge/
      │  one exported selector: call:args:requestId:
      ▼
NativeBackend (C#, NSObject)                  MediaArchive.Mobile/Platforms/iOS/
      │
      ▼
NativeApi → route table → handler             Services/Native/
      │  HomeQueries, CommonQueries, …         Services/Queries/, Logging/, Import/
      ▼
AppDbContext → mediaarchive.db                Data/
      │  JSON reply, correlated by request id
      ▼
MANativeApp.complete → continuation resumes
```

The contract types on both ends come from the C# records: `tools/SwiftGen` reflects
over `NativeRoutes.All` and writes `native/Sources/Generated/Contracts.swift` — the
`Codable` structs, the `String`-backed enums and the typed `Api`. A renamed C#
property fails the Swift build instead of emptying a screen.

---

## Folder-by-folder

### `Models/` — the domain
Plain C# classes; the shape of the data. `MediaItem` is an abstract base with
`Book` / `Game` / `Movie` / `Show` mapped to **one table** (Table-Per-Hierarchy);
`UserMediaItem` is my standing relationship to an item; `ConsumptionEntry` is one
pass through it and `EntryNote` one piece of writing during a pass. `Genre`, `Tag`,
`Person`, `Series` and `Universe` are the controlled vocabularies.

### `Data/` — persistence
`AppDbContext` (EF Core config: the TPH discriminator, the one-to-one to
`UserMediaItem`, join keys, unique indexes) and the design-time factory
`dotnet ef` uses.

### `Migrations/` — schema history
EF Core migrations, applied on launch in `MauiProgram.cs`. Regenerate with
`dotnet ef migrations add <Name>` after changing the models.

### `Services/` — the backend
| Folder | Role |
|---|---|
| `Queries/` | Read models per surface: `HomeQueries`, `LibraryQueries`, `DiaryQueries`, `ProfileQueries`, `CommonQueries`, plus `EffortMath` and the widget's `WidgetQueries` |
| `Logging/` | `LoggingService` — open, progress, finish and resume a pass |
| `UserItems/` | `UserItemService` — rating, favourite, classification, runtime |
| `Import/` | `MediaImportService` and `VocabularyResolver` — provider result → rows |
| `Providers/` | IGDB, OpenLibrary and TMDb clients behind `IMediaProvider` |
| `Infrastructure/` | `CoverCacheService`, `DeepLinkService` |
| `Native/` | **The boundary**: `Contracts.cs` (page-shaped records and args), `NativeRoutes.cs` (the route table), `NativeApi.cs` (dispatch + JSON) |

### `native/` — the UI (Swift)
```
native/
├── MediaArchiveUI.xcodeproj   one framework target over a synchronised Sources/ group
└── Sources/
    ├── Bridge/       Backend (the call + continuations), MANativeApp (ObjC entry), DateOnly, JSON, WidgetLink
    ├── Generated/    Contracts.swift — do not edit; run scripts/sync-contracts.sh
    ├── App/          RootView, Shell (app bar · tabs · custom tab bar), Router, Lexicon, Loadable
    ├── Theme/        Palette (generated from colors.json), Typography
    ├── Components/   CoverImage, StarRating, Blurb, VocabularyPicker, Controls, Confetti
    ├── Home/ Explore/ Library/ Diary/ Profile/ Item/    one store + views per surface
```

Stores are classes with identity and a lifecycle; contracts and view state are
structs. Views take what they are handed and render it — none decodes JSON or
reaches across the bridge.

### `MediaArchive.Mobile/` — the host
`MauiProgram.cs` (DI, migrations, the deep-link mapping), `App.cs`, `MainPage.cs`
(hosts the Swift root controller as a child view controller) and, under
`Platforms/iOS/`, `NativeBackend.cs`, `NativeHost.cs` and `WidgetSnapshotPublisher.cs`.
`appsettings.json` here holds the provider keys and is git-ignored.

### `widget/` — the home-screen widget
A Swift widget extension reading a snapshot the app writes into the shared App
Group. Built and embedded by `./ma`.

### Root files
| File | Role |
|---|---|
| `ma` | Project CLI — build/run on simulator or phone, renew signing, pull the phone's DB |
| `colors.json` | The palette, defined once; `scripts/sync-colors.sh` generates both Swift copies |
| `UiHelpers.cs` | The UI vocabulary — labels, units, glyphs, which contexts fit which type — served to Swift as the `lexicon` route |
| `tools/SwiftGen/` | The contract generator |
| `scripts/` | `new-branch.sh`, `sync-contracts.sh`, `sync-colors.sh` |
| `mediaarchive.db` | **Design-time only** — gives `dotnet ef migrations` a schema to diff. The live DB is on the phone. |

---

## Design system in one line

Media is visual, so **large cover art is the primary object**. Status, rating, and
favourite are small glyphs on the cover; the theme is a near-black ground with
forest-green panels, bone-white ink, Cinzel and EB Garamond.

Shared glyphs: `✓` completed · `▐▐` in progress · `○` interested · `✕` dropped ·
`★` rating · `♥` favourite.

---

## Git workflow (issues live in Obsidian)

There are no GitHub issues. Planning and design happen in the Obsidian vault at
`~/Documents/vault_personal` — hub note `Projects/MediaArchive.md`, one note per
unit of work in `Issues/`. A branch is cut from the issue note:

```bash
scripts/new-branch.sh "build the profile page"   # feat/build-the-profile-page, off latest main
```

The note's `kind:` picks the prefix (`feature → feat/`, `bug → fix/`,
`refactor → refactor/`). Commit in small steps, merge into `main` when the slice is
done, and close the note.
