# MediaArchive — iOS handoff (new Mac)

Paste this whole file into a fresh Claude Code session on the new Mac. It captures
where the project is, how to set the machine up, and the exact task to resume.

**What this is:** a personal, offline **iOS app** (.NET MAUI Blazor Hybrid, .NET 10)
for tracking everything I've consumed (books, games, films, TV) with on-device
SQLite. It was migrated from a desktop Blazor Web App. Single local user, no auth.

---

## 0. Read first — the moving parts that DON'T come with the clone
- **Active branch is `maui-blazor-hybrid`, not `main`.** All the MAUI + mobile work
  is there.
- **Secrets file is gitignored** → not in the clone. Recreate it (section 2).
- **Design references + the old Claude memory live in an Obsidian vault on the OLD
  Mac** (`~/Desktop/vault/...`) and won't clone. Copy them over if you want Claude
  to reference the mobile design (section 9).

---

## 1. Get the code
```bash
git clone https://github.com/bragehs/MediaArchive.git
cd MediaArchive
git checkout maui-blazor-hybrid
```

## 2. Recreate the secrets file (gitignored)
Create `MediaArchive.Mobile/appsettings.json` (copy it from the old Mac via AirDrop
if you still can — cleanest — otherwise paste your TMDB/IGDB keys into this shape):
```json
{
  "Tmdb":  { "ReadAccessToken": "<TMDB v4 read token>" },
  "Igdb":  { "ClientId": "<IGDB/Twitch client id>", "ClientSecret": "<IGDB/Twitch secret>" }
}
```
It's bundled as a MauiAsset and loaded via `AddJsonStream` in `MauiProgram.cs`. Keep
it out of git (already in `.gitignore`).

## 3. New-Mac toolchain
1. **.NET 10 SDK ≥ 10.0.302** — https://dotnet.microsoft.com/download/dotnet/10.0
   (this band carries Microsoft.iOS **26.5**, which matches Xcode **26.6**).
2. **MAUI workload:** `sudo dotnet workload install maui` (then `sudo dotnet workload update`).
3. **Xcode 26.6** — Mac App Store, or Apple Developer downloads for the exact `.xip`
   if the App Store offers something newer (a newer Xcode can re-trigger a version
   gate against the 26.5 iOS pack). Then:
   ```bash
   sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
   sudo xcodebuild -license accept
   ```
   Open Xcode once so it installs its components.
4. **Apple ID in Xcode** → Settings → Accounts → add your Apple ID ("Personal Team")
   — this is what mints the free signing certificate.

**Sanity check:**
```bash
dotnet --version                                   # >= 10.0.302
ls /usr/local/share/dotnet/packs | grep iOS.Sdk    # expect ...net10.0_26.5
xcode-select -p                                     # -> /Applications/Xcode.app/Contents/Developer
xcrun simctl list devices available | grep iPhone  # at least one simulator
```

## 4. Build & run
- **Simulator (CLI):** boot a simulator, then
  ```bash
  dotnet build MediaArchive.Mobile/MediaArchive.Mobile.csproj -t:Run -f net10.0-ios
  ```
- **Rider:** open `MediaArchive.sln` → run-config dropdown → **MediaArchive.Mobile**
  → pick a simulator/device in the target selector → Run.
- **Migrations** (the DbContext lives in the RCL, so pass both projects):
  ```bash
  dotnet ef migrations add <Name> --project MediaArchive.csproj --startup-project MediaArchive.csproj
  ```
- **Note:** root `dotnet run` no longer works — the root project is now a class
  library; the app is `MediaArchive.Mobile`.

## 5. Signing + device deploy (free Apple ID) — where we got stuck on the old Mac
The certificate part was done (it's per-machine, so redo on the new Mac via step 3.4).
The blocker was the **provisioning profile**, which is **per exact bundle id**:
1. In a **throwaway Xcode iOS app**, set **Bundle Identifier = `no.norapps.mediaarchive`**,
   **Automatically manage signing** + your Personal **Team**, select your iPhone,
   press Run once. That mints a profile for our bundle id (delete the throwaway app
   after).
2. On the iPhone: **Settings → Personvern og sikkerhet (Privacy & Security) →
   Utviklermodus (Developer Mode)** → on → restart. (The toggle only appears *after*
   a Mac has attempted to install a dev app onto the phone.)
3. Then **Settings → Generelt → VPN og enhetsadministrering (VPN & Device
   Management)** → trust the developer cert.
4. Now build **MediaArchive.Mobile** to the device. Free tier re-signs every ~7 days.

---

## 6. RESUME HERE — verify the cover-caching fix (was mid-flight)
**Problem it fixes:** covers used to render straight from the (slow/flaky) provider
URLs and were never saved, so OpenLibrary covers often never appeared and never
persisted. Just implemented, **committed but not yet built/verified** (old Mac's
Xcode vanished before I could build).

**What changed (all committed):**
- `MauiProgram.cs` — registers the real `CoverCacheService` (downloads covers to
  `FileSystem.AppDataDirectory/covers` on log, 8 s timeout; falls back to the remote
  URL on failure).
- `Services/CoverCacheService.cs` — returns `covers://c/<file>` as `LocalImagePath`.
- `MediaArchive.Mobile/Platforms/iOS/CoversSchemeHandler.cs` — a `WKUrlSchemeHandler`
  that serves those cached files to the WebView.
- `MainPage.xaml.cs` — registers the `covers://` scheme on the WebView config.
- `NullCoverCache` deleted; `ICoverCache` seam kept.

**To verify on the new Mac:**
1. Build + run on a simulator.
2. Log a book (e.g. "Dune") and a film. Then relaunch the app.
3. Home "Open now" / on-deck and the item page should show the covers, and they
   should **persist across relaunch** (proves they're cached locally, not re-fetched).
4. Check the console for any errors from `CoversSchemeHandler`.
- 27 host-side tests already pass (`dotnet test Tests/MediaArchive.Tests.csproj`),
  including the `covers://` path assertions.

---

## 7. Architecture (context for Claude)
- **Split:** root `MediaArchive.csproj` = **Razor Class Library** (`net10.0`) holding
  `Models/ Services/ Data/ Migrations/ Components/ wwwroot`. `MediaArchive.Mobile/` =
  thin **MAUI head** (`net10.0-ios`) referencing it; all DI in `MauiProgram.cs`.
  Bundle id `no.norapps.mediaarchive`, min iOS **18.0**.
- **Blazor Hybrid** (no render modes). Shell = `MainLayout` (fixed viewport, wordmark
  appbar, bottom tab bar with a center **+** FAB, iOS safe-area insets). The FAB opens
  **`AddSheet`** (bottom sheet: provider search → season picker for shows → capture
  form). Item detail page at `/item/{id}` (reached from Home covers).
- **CSS:** shared tokens + shell in `wwwroot/app.css`; each mobile surface's layout is
  in a **scoped `.razor.css`** with `m-`/`e-`/`i-` prefixes to avoid colliding with the
  1100-line desktop `app.css` (e.g. `.in` is a global input style, so item facts use
  `.ifn`/`.ifl`).
- **Providers** TMDB / IGDB / OpenLibrary via `IMediaProvider`; keys from the bundled
  `appsettings.json`. SQLite at `AppDataDirectory/mediaarchive.db`, migrated on launch.
  `dotnet ef` works via `Data/DesignTimeDbContextFactory.cs`.
- **TV = season-as-item (Option A):** a show fans into seasons (TMDB `seasons[]`); each
  season is a `Show` item with composite `ExternalId` `{showId}/season/{n}`,
  `Series` = show name, `SeriesPosition` = season number. Multi-season titles stored
  as "Show — Season N".
- **`CLAUDE.md`** (in the repo) has the working style: learning project, Zone-A
  (Services/Models/Data) is design-first + small diffs, few comments.

## 8. Known follow-ups (not blocking)
- Capture/log form still desktop-styled (mobile.html screen 10 restyle pending).
- Home doesn't auto-refresh after logging in the sheet (the `OnAdded` hook on
  `AddSheet` exists; needs an app-state signal to reach Home).
- Keyboard overlaps the bottom sheet (no viewport shift).
- Library / Diary / Profile tabs are inert (surfaces not built).
- DB export/backup via the iOS share sheet not built.

## 9. Design references (Obsidian vault — copy from old Mac, not in the repo)
- `~/Desktop/vault/Reference/Design drafts/mobile.html` — the mobile design source
  (screens: Home, Library, Genre drill, Diary, Profile, Item, Add·search, Add·log).
- `~/Desktop/vault/Issues/Migrate to MAUI Blazor Hybrid.md` — the original plan.
- Fonts (Cinzel / Cinzel Decorative / EB Garamond) are already self-hosted in the
  repo at `wwwroot/fonts/`.
