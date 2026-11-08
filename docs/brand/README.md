# Brand kit

The built-in Hostpanel logo: a glassy cube and the `hostpanel` wordmark. Every file below is derived
from the original artwork's paths, never redrawn. These are **defaults only**: an operator's uploads
(`portal.logo`, `portal.logo.dark`, `portal.logo.mark`, `portal.favicon`) replace them at runtime.

Colours: tile `#0B0B14`, share-card background `#0A0A0F`, wordmark `#0F172A` (light) / `#F0F4FF`
(dark), pinned-tab tint `#5E8EF3`.

## Sources (this folder)

| File | What it is | Used by |
|---|---|---|
| `mark.svg` | Cube only, transparent | `favicon.svg` in both apps; every mark PNG |
| `logo-light.svg` | Horizontal lockup, dark wordmark | `logo.svg` in both apps and the repository root |
| `logo-dark.svg` | Horizontal lockup, light wordmark | `logo-dark.svg` in both apps and the repository root |
| `logo-stacked-light.svg`, `logo-stacked-dark.svg` | Cube above wordmark | Not referenced yet |
| `app-icon.svg` | Mark and wordmark on the rounded dark tile | `apple-touch-icon.png`, `icon-192.png`, `icon-512.png` |
| `mark-64.png` … `mark-1024.png` | Mark at 64, 96, 128, 256, 1024 | Docs, slides, third-party listings |
| `avatar-400.png`, `avatar-1000.png` | The full original app icon | GitHub organisation and Docker Hub avatars |
| `social-preview.html` | 1280x640 source of the repository share card: cube mark and wordmark (`logo-dark.svg` inlined), headline, subline and stack badges | Source for `social-preview.png` |
| `social-preview.png` | 1280x640 render of `social-preview.html`, under 1 MB | The repository social preview: upload it by hand in GitHub, Settings, General, Social preview (GitHub has no API for it) |

## Served files (`client/public/` and `admin/public/`)

| File | Size | Linked from |
|---|---|---|
| `favicon.ico` | 16, 32, 48 | `<link rel="icon" sizes="any">` |
| `favicon.svg` | vector | `<link rel="icon" type="image/svg+xml">` |
| `favicon-16x16.png`, `favicon-32x32.png`, `favicon-96x96.png` | 16, 32, 96 | `<link rel="icon" sizes>` |
| `apple-touch-icon.png` | 180, full-bleed tile | `<link rel="apple-touch-icon">` |
| `icon-192.png`, `icon-512.png` | 192, 512 | `<link rel="icon">` (192) and the manifest, `purpose: any` |
| `icon-maskable-192.png`, `icon-maskable-512.png` | 192, 512, mark inside the safe zone | the manifest, `purpose: maskable` |
| `safari-pinned-tab.svg` | single-colour mark | `<link rel="mask-icon" color="#5E8EF3">` |
| `mstile-150x150.png` | 150 | `msapplication-TileImage` and `browserconfig.xml` |
| `browserconfig.xml` | — | `msapplication-config` |
| `logo.svg`, `logo-dark.svg` | vector | Default wordmark per theme: client `AppLogo` (`dark:` variant), admin `BrandLogo` (`light:` variant) |
| `client/public/logo.png` | 520×120 | schema.org `Organization.logo` |
| `client/public/og-image.jpg` | 1200×630 | `og:image` / `twitter:image` fallback (`utils/shareImage.ts`) |

The client emits its icon links from `client/utils/brandingIcons.ts` (built-in set) and its manifest
from `client/server/routes/site.webmanifest.ts`; the admin's are static in `admin/index.html`
and `admin/public/site.webmanifest`.

## Regenerating

Render the PNGs from these SVGs in a headless browser at exactly the listed pixel sizes, with a
transparent background except the tile, maskable, share-card and avatar images. Build `favicon.ico`
from the 16, 32 and 48 renders as PNG-in-ICO. Keep the file names: code and manifests reference them.

### Social preview

Open `social-preview.html` in a headless browser at a 1280x640 viewport and screenshot the page (clip 0, 0, 1280, 640) to `social-preview.png`. The page needs no network: the wordmark is the outlined SVG, and the text uses Inter when installed, otherwise the system sans-serif. Check the result by eye, then upload the PNG in the repository settings.
