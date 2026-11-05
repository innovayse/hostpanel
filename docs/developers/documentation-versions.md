---
title: Documentation versions
description: How the documentation is published per release, what the version switcher does and which build settings control it.
---

# Documentation versions

The documentation exists once per minor release, so a reader on an older release reads pages that match it.

| Address | What it is |
|---------|------------|
| `/` | The latest stable release. This is the version search engines index. |
| `/vX.Y/` | One build per older minor release, made from the newest patch tag of that minor. |
| `/next/` | The `develop` branch, not released yet. It is marked `noindex` and has no sitemap. |

A version switcher in the header lists them. The list is read at run time from `versions.json`, which is published next to the latest docs, so an older build shows releases made after it without being rebuilt.

## What an older version looks like

- A notice at the top says which version it is and links to the same page in the latest docs.
- Each page has a canonical link to the same page in the latest docs, or to the latest home page when the page no longer exists there.
- A page that no longer exists in the latest docs also gets `noindex, follow`.
- Only the latest docs have a sitemap.

## Which releases have versioned docs

The switcher and the rules above live in `docs/.vitepress/versions.ts`. A release tag cut before that file existed cannot show the switcher, so it is not published as a version. The list starts at the first release tag that contains the file. Until a release tag contains it, the documentation is built from the configured ref as before, and only `next` is added. Tags with a suffix such as `-rc.1` are not stable releases and are ignored.

## Build settings

Read by `docs/.vitepress/config.ts`. All are optional; a plain build without them is an unversioned site.

| Variable | Meaning |
|----------|---------|
| `DOCS_VERSION` | `vX.Y` for a release, `next` for the development docs, empty for an unversioned build. |
| `DOCS_LATEST_VERSION` | The label of the latest release. A build whose `DOCS_VERSION` equals it is the latest. |
| `DOCS_LATEST_URL` | Public address of the latest docs. Required for an older release and for `next`. |
| `DOCS_VERSIONS_URL` | Where the browser fetches `versions.json` from. Without it the switcher stays hidden. |
| `DOCS_LATEST_PAGES` | Path of a JSON array with the page paths of the latest docs. Without it every page of an older release points at the latest home page and is marked `noindex`. |
