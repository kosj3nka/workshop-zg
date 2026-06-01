# Images Required

All images go in `wwwroot/images/` unless noted otherwise.
Recommended format: WebP (better compression) or JPEG. PNG only for logos with transparency.

| File | Page / Component | Notes |
|------|-----------------|-------|
| `logoName.png` | Navbar + Footer | Logo with name. PNG with transparent background. Footer version: CSS `filter: invert(1) brightness(2)` makes it white. |
| `justLogo.png` | Homepage hero | Icon-only logo (no text). Shown centred over video. CSS makes it white. |
| `hero-poster.jpg` | Homepage hero | Static poster shown while video loads. Same frame as first video clip. ~1920×1080. |
| `vibe1.jpg` | Homepage photo strip | Café/workshop atmosphere shot |
| `vibe2.jpg` | Homepage photo strip | Café/workshop atmosphere shot |
| `vibe3.jpg` | Homepage photo strip | Café/workshop atmosphere shot |
| `mood1.jpg` | Homepage mood split | Full-width editorial. People at workshop or café. ~2400×1200. |
| `quad1.jpg` | Homepage quad grid | Detail or texture shot |
| `quad2.jpg` | Homepage quad grid | Detail or texture shot |
| `quad3.jpg` | Homepage quad grid | Detail or texture shot |
| `quad4.jpg` | Homepage quad grid | Detail or texture shot |
| `about-hero.jpg` | O nama — hero banner | Wide establishing shot of the space. ~2400×900. |
| `about-founders.jpg` | O nama — founders | Portrait of Marta & Luka |
| `about-space1.jpg` | O nama — photo trio | Space detail |
| `about-space2.jpg` | O nama — photo trio | Space detail |
| `about-space3.jpg` | O nama — photo trio | Space detail |
| `about-wide.jpg` | O nama — full-width | Long horizontal space or workshop shot. ~2400×800. |
| `about-detail1.jpg` | O nama — detail grid | Close-up: materials, tools, coffee |
| `about-detail2.jpg` | O nama — detail grid | Close-up: materials, tools, coffee |
| `about-detail3.jpg` | O nama — detail grid | Close-up: materials, tools, coffee |
| `about-detail4.jpg` | O nama — detail grid | Close-up: materials, tools, coffee |
| `collab-workshop.jpg` | Suradnja — workshop section | Guest-host workshop scene |
| `collab-marketing.jpg` | Suradnja — marketing section | Product on shelves / signage |

---

## Video Files

Location: `wwwroot/videos/`

| File | Purpose |
|------|---------|
| `hero1.webm` | Hero loop clip 1 — WebM (better browser compression) |
| `hero1.mp4` | Hero loop clip 1 — MP4 fallback |
| `hero2.webm` | Hero loop clip 2 (optional, for variety) |
| `hero2.mp4` | Hero loop clip 2 fallback |

**Video specs**: 1080p minimum, ~5–15 sec loops, no audio needed. Muted autoplay only works with `muted` attribute set.

---

## Workshop Icons (Blob Storage)

These are uploaded by owners via the Admin panel — **not** in the repo.
Each `Workshop` record stores the Blob URL in `IconFileName`.

Owners should upload PNG icons at ~200×200px for calendar display.

---

## Placeholder Images for Development

Until real photos are provided, use:
- [Unsplash](https://unsplash.com/s/photos/cafe-workshop) for café / workshop vibes
- [Lorem Picsum](https://picsum.photos/800/600) for quick layout testing: `<img src="https://picsum.photos/800/600?random=1">`
