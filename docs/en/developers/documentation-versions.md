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

## How the switcher opens a page

`versions.json` is a list of entries, each with a `label` (`v0.3`, `next`), a `link` (the path of that version's home page, with a trailing slash) and a `kind` (`latest`, `release` or `next`). The file is read in the browser and is not trusted:

- An entry whose `link` is not a path of the docs site (an address with a host, `//host`, a path with `..`, a path outside the root of the docs, or a path with a backslash, a full width slash or backslash, a null, or an encoded slash, backslash or null such as `%2f`, `%5c` or `%00` in any letter case, however many times it is encoded, as in `%252f`) is ignored, so it never reaches the list. A link is decoded until it stops changing, and one that cannot be decoded, such as an overlong `%c0%af`, is ignored too. An entry that repeats the label of an earlier one is ignored as well, and only the first 100 entries of the file are read.
- Choosing a version opens the same page there when that page exists, and the home page of that version when it does not. When the list is opened the switcher asks the server for the page in each other version, with `HEAD`, and asks again with `GET` only when the server answers 405 or 501. Redirects are not followed.
- Only a final answer decides that a page is missing: any status that is not a success and not a passing failure, such as 404, 403 or 400, or a redirect. A server that answers `HEAD` with 403 or 400 therefore gets the home page link. A failed request, no answer within four seconds, and a 5xx, 408 or 429 status are not an answer about the page. The link goes to the home page of that version for now, and the page is asked for again the next time the list is opened.
- The lookup is bounded. At most 20 versions are asked, 4 requests at a time, and a version past the limit links to its home page. Final answers (the page is there, or it is not) are kept per page for the visit, so opening the list again does not ask again. Answers that are not final are never kept. Closing the list, opening another page or leaving the page stops the requests that are still running.

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

## Languages

Every version is built in all three languages (English, Russian and Armenian) in one build, so `/vX.Y/ru/` and `/next/hy/` exist next to `/vX.Y/`. A release cut before the languages were added has English only. See [Translating the documentation](./translating-docs).
