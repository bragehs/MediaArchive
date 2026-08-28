# CLAUDE.md

## What this project is

A personal, locally-run **media OS** — one place tracking everything I've consumed
(books, games, films, shows, anime) with taste insights. It owns the DB and
business logic directly: no separate API over HTTP, no auth, single local user.
See `README.md` for the full layout and `Migrations/` for schema history.

**There is no web app.** `MediaArchive.csproj` is a **Razor class library** —
components, services, models, EF Core — with exactly one head on top of it:
`MediaArchive.Mobile`, a **.NET MAUI Blazor Hybrid iOS app**. The library has no
`Program.cs` and cannot be run on its own.

**Stack:** .NET MAUI + BlazorWebView · .NET 10 · EF Core 10 + SQLite · Razor
components inject services / `DbContext` directly.

**Where the database lives:** on the phone, at `FileSystem.AppDataDirectory/
mediaarchive.db`. The `mediaarchive.db` in the repo root is **design-time only** —
it exists so `dotnet ef migrations` has a schema to diff against. `./ma pull`
refreshes it from the phone; treat the phone as the source of truth.

## Important

Dont write so many comments when coding, only when necessary.

## This is a learning project — read this before writing code

I'm **new to C#** and building this to learn. My day job is **backend ASP.NET Core
with controller APIs**, so the transferable skills — the C# language, EF Core, DI,
the service layer, async, LINQ, architecture — matter to me far more than the Blazor
UI. Optimize our collaboration for *me learning*, not for you shipping fast.

**Now that I've started the job, I get plenty of hands-on C# typing every day at
work.** So this project's role has shifted: the learning I want from it is the
*design reasoning* and the *code review* reps, not the keyboard time. Optimize for
energy-to-learning ratio — I'm doing this in addition to a full workday.

### The learning split — this is the important part

Treat these two zones of the codebase differently:

**Zone A — the C# I care about (design together, you implement, I review):**
`Services/`, `Models/`, `Data/` (EF Core config, `DbContext`, migrations), business
logic, LINQ queries, DI wiring in `Program.cs`, and any external-API import clients.
For this code:

- **Discuss the design first.** When a decision comes up (where a responsibility
  lives, service boundaries, how to model data, sync vs async, how to structure an
  import service), **stop and lay out the options and trade-offs with a
  recommendation** before any code. This architectural thinking is the primary
  learning goal, not overhead.
- **Once we've agreed on the approach, you write the implementation.** Keep it in
  small, readable slices (see Small diffs) so I can review each one.
- **Explain as you go.** Name the C#/.NET concepts in play so I build the mental
  model from reading your code (see Explain the C#).
- **Expect me to review and push back.** Treat my review comments as the main event:
  answer them, and flag anything in your own diff worth a second look — naming,
  correctness, idiomatic C#, EF Core pitfalls (N+1, context lifetime, tracking).
- If something is a genuinely new concept I say I want in my fingers, I'll ask to
  hand-write that piece — offer to guide me instead of writing it then.

**Zone B — the Blazor front-end (lower priority, you can drive):**
`Components/`, `.razor` files, `wwwroot/`, CSS, `UiHelpers.cs`. Learning the Razor
component model is not my focus. You may write this more freely — but still keep
diffs readable and tell me briefly what you did so I can follow the wiring between
UI and services.

### Working rules

- **Explain the C#.** When you write or review Zone-A code, name the language/
  framework concepts in play (async/await, LINQ, generics, DI lifetimes, EF Core
  relationships, nullable reference types) so I build the mental model.
- **Small diffs.** Never drop a large feature in one go. Work in slices I can read
  and explain back — this is what keeps the review valuable now that you write it.
- **Design before code.** Surface architectural forks explicitly with a
  recommendation *and* the reasoning, and settle the approach with me before
  implementing a Zone-A feature.
- **Design choices**. Look at desktop/vault/Personal Projects/Mediaarchive and look for relevant design choices before
  implementing a task.
- Dont write to much comments in the code, only when necessary.

## Roadmap context

Building one vertical slice — **Log → Library → item detail** — end-to-end before
Diary/Profile. Current focus: **Phase 3 (Log & capture)** — universal add flow that
imports from IGDB / Open Library via a C# service. Phases 1–2 (unified TPH schema +
Blazor app with read-only surfaces) are done.

## Build & run

```bash
./ma                                # run on the iOS simulator (no Rider needed)
./ma phone                          # renew signing, build, install + launch on my iPhone
./ma pull                           # copy the phone's DB + covers back into the repo
./ma renew                          # refresh the 7-day provisioning profile
dotnet ef migrations add <Name>     # after changing Models/ or DbContext
dotnet build                        # compile check
```

`./ma --help` lists everything. Migrations are applied on app launch
(`MauiProgram.cs`); there is no seeding step any more.

**Signing:** a free Apple ID only gets **7-day** provisioning profiles, so device
builds break weekly with "Could not find any available provisioning profiles".
`./ma renew` fixes that non-interactively by driving `xcodebuild
-allowProvisioningUpdates` against a generated stub Xcode project in
`.provisioning/`. `./ma phone` renews automatically when the profile is nearly
expired. A launchd agent runs `./ma weekly` (pull + redeploy) every Tuesday at
10:00 — install or remove it with `scripts/install-weekly-job.sh`.
