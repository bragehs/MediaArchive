# MediaArchive

A personal, locally-run **media OS**: one place that tracks everything I've consumed
across every media type (books, games, films, shows, anime) and surfaces taste
insights. Letterboxd + Goodreads + IGDB combined, but private, local, and unified.

This is **one application** — a Blazor Web App that also owns the database and
business logic. There is no separate backend/frontend, no API over HTTP, and no
authentication: it runs as a single local user.

---

## Stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core **Blazor Web App**, Interactive Server render mode |
| Runtime | .NET 10 |
| Data access | EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`) |
| Database | **SQLite** — a single `mediaarchive.db` file, created + seeded on first run |
| UI | Razor components + one hand-written CSS design system (`wwwroot/app.css`) |
| Auth | None (single local user) |

## Running it

```bash
dotnet run        # builds, migrates + seeds the DB, starts the server
```

Open the URL it prints (e.g. `http://localhost:5064`). To wipe and reseed the data,
delete `mediaarchive.db*` and run again.

```bash
dotnet ef migrations add <Name>   # after changing Models/ or DbContext
dotnet build                      # compile check
```

---

## How a page gets its data

The whole point of the Blazor design: no HTTP hop between UI and data.

```
Razor component (Components/Pages/*.razor)
      │  @inject LibraryService
      ▼
LibraryService (Services/)          ← query methods, returns entities
      │  IDbContextFactory<AppDbContext>
      ▼
AppDbContext (Data/)                ← EF Core, maps to…
      ▼
mediaarchive.db (SQLite)
```

A component injects `LibraryService`, calls an `async` method, gets back model
objects, and renders them. Same C# types (`MediaItem`, `UserMediaItem`) flow from
the database all the way into the markup.

---

## Folder-by-folder

### `Models/` — the domain
Plain C# classes; the shape of the data.

| File | What it holds |
|---|---|
| `MediaItem.cs` | Abstract base + `Book` / `Game` / `Movie` / `Show` / `Anime` subclasses. EF maps this whole hierarchy to **one table** (Table-Per-Hierarchy) so "everything I've consumed" is a single query. Type-specific creators (`Author`, `Developer`, …) live on the subclasses and surface through a common `Creator`. |
| `UserMediaItem.cs` | My *standing relationship* to one item — exactly one per item: status, rating, favourite, tags, notes, added-date. No `UserId` (single user). |
| `ConsumptionEntry.cs` | One row *per pass* through an item. This is what makes re-reads/replays, rating drift, and honest time-invested stats possible. |
| `Genre.cs` | `Genre` + the `MediaItemGenre` join (many-to-many). |
| `Universe.cs` | Optional cross-media grouping (e.g. "The Witcher" spans a book + a game). |
| `Enums.cs` | `MediaType` (also the TPH discriminator) and `MediaStatus`. |

### `Data/` — persistence
| File | Role |
|---|---|
| `AppDbContext.cs` | The EF Core context. `DbSet`s + `OnModelCreating` config: the TPH discriminator, the one-to-one `UserMediaItem`↔`MediaItem`, the genre join key, unique indexes. |
| `DbSeeder.cs` | Template/skeleton data (24 items across all five types, with genres, universes, and consumption history) so the surfaces have something to show. Runs once, on startup, if the DB is empty. |

### `Migrations/` — schema history
EF Core-generated migrations. `InitialCreate` builds the whole schema. Applied
automatically at startup (`db.Database.Migrate()` in `Program.cs`). Regenerate with
`dotnet ef migrations add <Name>` after changing the models.

### `Services/` — the read layer
| File | Role |
|---|---|
| `LibraryService.cs` | All the queries the UI needs: the filtered Library, a single item's detail, currently-consuming, the diary feed, and aggregate profile stats. Uses `IDbContextFactory` (each call gets its own short-lived context — the recommended Blazor Server pattern). |

### `Components/` — the UI (Blazor)
```
Components/
├── App.razor            root HTML document (<head>, script tags, CSS links)
├── Routes.razor         the router + default layout
├── _Imports.razor       global @using for every component
├── Layout/
│   ├── MainLayout.razor  the sidebar + content shell
│   ├── NavMenu.razor     the five-surface sidebar navigation
│   └── ReconnectModal    Blazor Server reconnect UI (framework default)
├── Pages/               one routable component per surface
│   ├── Home.razor        "/"         currently-consuming, recent activity, lore widget
│   ├── Log.razor         "/log"      universal add flow
│   ├── Library.razor     "/library"  unified collection; status/type are FILTERS
│   ├── Diary.razor       "/diary"    chronological consumption feed
│   ├── Profile.razor     "/profile"  taste dashboard (aggregate stats)
│   ├── ItemDetail.razor  "/item/{id}" one item's record + its consumption history
│   ├── Error.razor / NotFound.razor  framework error pages
└── Shared/              reusable UI primitives
    ├── MediaCard.razor   a poster tile for the walls (cover + status/rating glyphs)
    └── CoverArt.razor    cover image, with a typographic fallback tile when there's no art
```

**The five surfaces** map to distinct jobs. The rule: *a tab is a distinct job; a
filter is the same job sliced.* So Library is one collection and status/type/genre
are filters on it — not separate tabs.

### `wwwroot/` — static assets
| Item | Role |
|---|---|
| `app.css` | The whole design system: dark theme, CSS variables, the poster-wall grid, cards, glyph badges, filter chips, stat bars, the diary timeline. Cover-first — artwork carries the UI. |
| `lib/bootstrap/` | Bootstrap (from the template) — used only for reset/grid; the look is `app.css`. |
| `favicon.png` | Tab icon. |

### Root files
| File | Role |
|---|---|
| `Program.cs` | App entry point: registers Blazor, the SQLite `DbContextFactory`, and `LibraryService`; sets up the HTTP pipeline; migrates + seeds the DB; runs. |
| `UiHelpers.cs` | Presentation helpers — maps `MediaStatus` / `MediaType` to glyphs, labels, and CSS classes (the shared visual vocabulary). |
| `MediaArchive.csproj` | Project + NuGet package references. |
| `MediaArchive.sln` | Solution file. |
| `appsettings*.json` | Config (the SQLite connection string). Git-ignored; `Program.cs` falls back to `mediaarchive.db` if absent. |
| `Properties/launchSettings.json` | Local run profiles (ports). |
| `.gitignore` | Ignores `bin/`, `obj/`, `appsettings*.json`, and the `*.db` files. |

---

## Design system in one line

Media is visual, so **large cover art is the primary object**. Status, rating, and
favourite are small glyphs on the cover; filtering is an unobtrusive bar above the
gallery. When an item has no cover, a **typographic fallback tile** (title on a colour
block keyed to media type) keeps the wall from breaking.

Shared glyphs: `✓` completed · `▐▐` in progress · `○` interested · `✕` dropped ·
`★` rating · `♥` favourite.

---

## Git workflow (issues live in Obsidian)

There are no GitHub issues. Planning and design happen in the Obsidian vault, under
`Personal Projects/MediaArchive/Issues/*.md` (each note has `fileClass: issue`
frontmatter). A branch is named after the slug of the issue note it implements —
e.g. the note `Log and capture.md` → branch `log-and-capture`.

Start work on an issue:

```bash
scripts/new-branch.sh "Log and capture"   # finds the vault note, branches off latest main
```

The script slugifies the note's title, pulls the latest `main`, and creates (or
switches to) the matching branch. It's just a convenience wrapper around:

```bash
git switch main && git pull        # start from latest main
git switch -c log-and-capture      # branch named after the vault note's slug
```

Then commit in small steps and push:

```bash
git push -u origin log-and-capture   # -u sets tracking the first time
```

Merge into `main` when the slice is done — either a PR on GitHub, or locally:

```bash
git switch main && git merge log-and-capture && git push
git branch -d log-and-capture        # delete the merged branch
```

Finally, mark the issue note's `status:` frontmatter `completed` in Obsidian.
