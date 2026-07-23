# CLAUDE.md

## What this project is

A personal, locally-run **media OS** — one place tracking everything I've consumed
(books, games, films, shows, anime) with taste insights. One **Blazor Web App**
(Interactive Server, .NET 10) that owns the DB and business logic directly: no
separate API over HTTP, no auth, single local user. See `README.md` for the full
layout and `Migrations/` for schema history.

**Stack:** ASP.NET Core Blazor Web App · .NET 10 · EF Core 10 + SQLite
(`mediaarchive.db`) · Razor components inject services / `DbContext` directly.

## Important

Dont write so many comments when coding, only when necessary.

## This is a learning project — read this before writing code

I'm **new to C#** and building this to learn. My day job is **backend ASP.NET Core
with controller APIs**, so the transferable skills — the C# language, EF Core, DI,
the service layer, async, LINQ, architecture — matter to me far more than the Blazor
UI. Optimize our collaboration for *me learning*, not for you shipping fast.

### The learning split — this is the important part

Treat these two zones of the codebase differently:

**Zone A — the C# I want to own (teach, don't write):**
`Services/`, `Models/`, `Data/` (EF Core config, `DbContext`, migrations), business
logic, LINQ queries, DI wiring in `Program.cs`, and any external-API import clients.
For this code:

- **Do NOT write it for me unless I explicitly ask.** Default to explaining the
  *approach*, the trade-offs, and the relevant C#/.NET idiom — then let me write it.
- Prefer Socratic hints and small examples over finished solutions.
- After I write something, review it and push on it: naming, correctness, idiomatic
  C#, EF Core pitfalls (N+1, context lifetime, tracking).
- When a design decision comes up (where a responsibility lives, service boundaries,
  how to model data, sync vs async, how to structure an import service), **stop and
  discuss the options and trade-offs with me** before any code. This architectural
  thinking is a primary goal, not overhead.

**Zone B — the Blazor front-end (lower priority, you can drive):**
`Components/`, `.razor` files, `wwwroot/`, CSS, `UiHelpers.cs`. Learning the Razor
component model is not my focus. You may write this more freely — but still keep
diffs readable and tell me briefly what you did so I can follow the wiring between
UI and services.

### Working rules

- **Explain the C#.** When you do write or review Zone-A code, name the language/
  framework concepts in play (async/await, LINQ, generics, DI lifetimes, EF Core
  relationships, nullable reference types) so I build the mental model.
- **Small diffs.** Never drop a large feature in one go. Work in slices I can read
  and explain back.
- **Make me participate.** If I ask you to "just build" a Zone-A feature, offer to
  guide me through writing it instead, unless I clearly want it done for me.
- **Surface design choices, don't bury them.** Call out architectural forks
  explicitly and give me a recommendation *with* the reasoning.
- **Design choices**. Look at desktop/vault/Personal Projects/Mediaarchive and look for relevant design choices before
  implementing a task.
- Dont write to much comments in the code, only when necessary.

## Roadmap context

Building one vertical slice — **Log → Library → item detail** — end-to-end before
Diary/Profile. Current focus: **Phase 3 (Log & capture)** — universal add flow that
imports from IGDB / Google Books via a C# service. Phases 1–2 (unified TPH schema +
Blazor app with read-only surfaces) are done.

## Build & run

```bash
dotnet run                          # builds, migrates + seeds the DB, starts the server
dotnet ef migrations add <Name>     # after changing Models/ or DbContext
dotnet build                        # compile check
```

Wipe + reseed: delete `mediaarchive.db*` and run again. The app applies migrations
and seeds on startup (`Program.cs`).
