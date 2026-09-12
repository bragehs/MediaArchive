# CLAUDE.md

## What this project is

A personal, locally-run **media OS** — one place tracking everything I've consumed
(books, games, films, shows) with taste insights. It owns the DB and business logic
directly: no separate API over HTTP, no auth, single local user. See `README.md` for
the full layout and `Migrations/` for schema history.

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

## The Obsidian vault is the memory

Project memory lives in the vault at `~/Documents/vault_personal`, **not** in this
file and not in the chat. Hub note: `Projects/MediaArchive.md`. Read
`~/Documents/vault_personal/CLAUDE.md` for the vault's schema (note types,
frontmatter, tags, templates).

The rule: **a decision that isn't written in the vault didn't happen.** Next session
starts with no context beyond what's on disk.

**Before starting any task:**
1. Read the hub `Projects/MediaArchive.md` — scope, surfaces, open decisions.
2. Find the matching note in `Issues/` (they're flat; `status` there is the real
   roadmap, not a list in this file). Read it.
3. Read the `Reference/` notes it links — `Data model`, `External data providers`,
   `UI - Surfaces and navigation` — before touching anything they cover.
4. No issue note for the work? Create one from `_Templates/issue.md` first, with
   `project: "[[MediaArchive]]"`, and get it agreed before writing code.

**Every piece of work starts as a note and a branch, in that order.** No exceptions for
"this is small" — small things are exactly what gets lost. The note comes first because
it settles the scope; the branch comes from the note, so the two can't drift:

```bash
scripts/new-branch.sh "build the profile page"
```

That reads the note's `kind:` and cuts the branch off the latest `main` —
`feature → feat/`, `bug → fix/`, `refactor → refactor/`. Never commit to `main`
directly.

**While working:** the issue note is where the design lands.
- `## Done when` — acceptance criteria. The scope gate; agree this before code.
- `## Approach` — the chosen path **and why**, including options rejected.
- `## Open questions` — unknowns and blockers. Non-empty means not ready to ship.

**When work lands:** tick `Done when` + `Before merge`, set `status`, file follow-ups
as new issue notes rather than leaving them in the chat, and update the `Reference/`
note if the shape of the data or the IA changed. **Commit the vault** — it's a git
repo and its own CLAUDE.md asks for it.

**The vault describes what exists, not what was once planned.** It has drifted behind
the code before — a surface specced one way and built another, fields that were
renamed or dropped, plans marked `todo` long after they shipped. So:

- **The code wins.** When a note and the codebase disagree, the note is wrong. Fix the
  note as part of the task; don't implement to a stale spec, and don't leave a spec
  standing that describes something else.
- **Verify before you trust a note.** A note naming a field, service, or migration is
  a claim to check against the source, not a fact.
- **Issues are events; `Reference/` notes are state.** An issue records one unit of
  work at a point in time: once it is `completed` it is **closed and never rewritten**.
  Work that changes that surface later is a **new issue**, so the sequence of issues
  *is* the project's history — which is why they carry `started:` / `completed:` dates
  and are named for the work ("Build the profile page", "Redesign the profile page"),
  not for the surface. A `Reference/` note is the opposite: it describes what is true
  **now**, so a superseded decision there gets rewritten, not appended to. Keep the
  reasoning that still explains the code; the chain of issues holds how it got there.
- **Don't carry dead weight.** Speculative specs, provider comparisons that were
  settled, and migration plans that ran are noise once they're true or false. Cut them.
- **Real features get a note.** Something shipped with no note in the vault is the
  same drift in the other direction.

## How we work

**Draft the solution before implementing it.** For anything touching `Services/`,
`Models/`, `Data/` (EF config, `DbContext`, migrations), DI wiring, or the external
provider clients: stop, lay out the options with trade-offs and a recommendation, and
settle it with me first. Write the agreed approach into the issue note, then build.
I want to be an active part of these choices — don't collapse a fork on your own.

**You can drive the UI.** `Components/`, `.razor`, `wwwroot/`, CSS, `UiHelpers.cs` —
implement directly and tell me briefly how it wires to the services. Still flag it if
a UI need implies a service or schema change; that's back to the paragraph above.

**Small diffs.** One reviewable slice at a time — never a whole feature in one drop.

**Push back.** Flag anything in your own diff worth a second look: naming, N+1s,
context lifetime, tracking, nullability, error and empty states.

## Code standards

- **Simple over clever.** The plainest thing that fully solves the problem. No
  abstraction for a second caller that doesn't exist yet.
- **Reuse before adding.** Look for the existing service, query, or component first.
  Extending one beats a near-duplicate.
- **Keep the codebase small.** A feature that lands as a large net addition is a
  design smell — say so and propose the smaller shape. Deleting counts as progress.
- **Refactor to keep it readable.** When a file, method, or component stops being
  easy to read, pull it apart as part of the work — not as a someday issue. Flag it
  first if the refactor is bigger than the change that triggered it.
- **The code explains itself.** Clear names and small methods instead of narration.
- **Comments only where they carry information the code can't** — a non-obvious
  why, a workaround, a sharp edge. **Never more than one line.** No comments that
  restate the next statement, no section banners, no XML doc blocks on obvious
  members.
- Match the surrounding style; `Tests/` covers providers and caching — extend it
  when you touch that logic.

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
