# Branding

The MediaArchive mark: an illuminated **MA** monogram in Cinzel Decorative —
bone **M** ("Media") + leaf-green **A** ("Archive") — on the forest squircle,
framed by marigold corner brackets from the vitrine's ornament. Colours track
the app tokens (`--ac`, `--ink`, `--ac2`, `--panel`).

| File | What |
|---|---|
| `make_icon.py` | Pillow generator → `icon_1024.png` (+ smaller PNGs) |
| `CinzelDecorative-Bold.ttf` | the wordmark font (OFL), bundled so the build is offline |
| `build.sh` | render → pack `MediaArchive.icns` → install into the app + favicon |
| `icon_1024.png` | the master render (checked in for reference) |
| `MediaArchive.icns` | the packed macOS icon |

## Regenerate

```bash
cd branding
./build.sh
```

That rebuilds `MediaArchive.icns`, refreshes `wwwroot/favicon.png`, and installs
into `~/Applications/MediaArchive.app`. Tweak colours/letters/size in
`make_icon.py` first.

The **in-app masthead emblem** is separate — it's pure CSS (`.brandmark` in
`wwwroot/app.css`, markup in `Components/Layout/NavMenu.razor`), using the same
loaded Cinzel Decorative font, so it needs no image and stays theme-consistent.

The font is licensed under the SIL Open Font License; redistribution here is
permitted under those terms.
