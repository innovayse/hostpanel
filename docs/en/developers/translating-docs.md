---
title: Translating the documentation
description: How the documentation is organised in English, Russian and Armenian, how to translate a page and which words stay untranslated.
---

# Translating the documentation

The documentation is published in English, Russian and Armenian. English is the source language and sits at the root of the site, so its addresses never carry a language prefix. Russian is served below `/ru/` and Armenian below `/hy/`.

To translate the Hostpanel application itself (the admin panel, the client portal and the API messages), see [Translating Hostpanel](./translating).

## How the folders are laid out

Every language keeps its pages in a folder of its own, and the Russian and Armenian folders mirror the English tree, file for file:

| English page | Russian page | Armenian page |
|--------------|--------------|---------------|
| `docs/en/index.md` | `docs/ru/index.md` | `docs/hy/index.md` |
| `docs/en/staff/orders.md` | `docs/ru/staff/orders.md` | `docs/hy/staff/orders.md` |
| `docs/en/operators/index.md` | `docs/ru/operators/index.md` | `docs/hy/operators/index.md` |

The English folder is not part of the address: `docs/en/staff/orders.md` is served at `/staff/orders`, `docs/ru/staff/orders.md` at `/ru/staff/orders`. Files that belong to no language (`public`, `scripts`, `tests`, `.vitepress` and the package files) stay at the root of `docs`.

A page that is not translated yet does not return a "not found" error. At build time Hostpanel writes a placeholder page for every English page a language does not have. The placeholder shows a short notice in the reader's language and the English text below it. Search engines are told not to index it and to treat the English page as the original. The placeholders are generated into `docs/_fallback/`, which git ignores, so they are never committed. When you add the real page, it replaces the placeholder on the next build.

These are not translated and have no placeholders: the `components` page and the generated fragments in `api-reference/_partials`. The `docs/brand` folder holds the brand kit and is not a page of any language. The list is `untranslated` in `docs/.vitepress/i18n/locales.json`.

## Translate a page

1. Copy the English file (`docs/en/...`) to the same path in `docs/ru/` or `docs/hy/`.
2. Translate the `title` and `description` in the front matter and the text of the page. Leave every other front matter key as it is.
3. Start every internal link with the language folder: `[Orders](/ru/staff/orders)` in a Russian page, `[Orders](/hy/staff/orders)` in an Armenian page. A link to a page that is never translated (such as `/components`) stays as it is.
4. Keep the anchor of every heading the same as in English by adding it by hand, for example `## Dashboard {#dashboard}`. Other pages link to those anchors, and the generated anchor of a translated heading would be different.
5. Run the checks (see below) and open the page in the dev server.
6. Add the page title to the sidebar if the page is new in English. The sidebar titles of all languages live in `docs/.vitepress/i18n/en.json`, `ru.json` and `hy.json` under `pages`, keyed by the page link.

Pages are translated whole. Do not leave half of a page in English: a page that is only partly translated is worse for the reader than the placeholder.

## What stays untranslated

Keep these exactly as they are in English, in every language:

- Code, commands, file names, paths, environment variable names and configuration keys.
- Command line flags and their values.
- API paths, HTTP methods, status codes, JSON field names and error codes.
- Product and technology names such as Hostpanel, Docker, Nginx, cPanel, WHMCS, SSH, SSO and OpenAPI.
- The names of buttons and menu items **in the application** when the application itself shows them in English. Where the application has a Russian or Armenian interface, use the word the interface uses, so the reader can find it on the screen. The wording of the interface is in `admin/src/locales/` and `client/locales/`.

Translate the text inside code comments only when the comment explains something to the reader. Do not translate the code around it.

## Use the glossary

The glossary at `docs/.vitepress/i18n/glossary.json` lists the core terms (plugin, invoice, provisioning, server, domain, client, staff and so on) in the three languages. The words match the admin panel and client portal, so the documentation and the screens use the same name for the same thing. Use the glossary word every time; do not invent a second word for a term. When a term is missing, add it to the glossary in the same change as the page that needs it.

Write "you" in the polite form in Russian (вы) and in Armenian (դուք). Keep sentences short and in the active voice, as in the English text.

## Documentation components

The components (`Callout`, `Steps`, `SettingTable`, `Label`, `Screenshot`, `DemoLink`) work the same in every language. Their built-in words (the column names of `SettingTable`, the text of `Label`, the buttons of the image viewer) come from the language files in `docs/.vitepress/i18n/`. Translate the text you put inside them, not their names or attributes.

## The text of the site itself

The navigation bar, the sidebar, the search box, the "On this page" title, the previous and next links, the 404 page and the texts of the theme components are not in the Markdown files. They are in `docs/.vitepress/i18n/en.json`, `ru.json` and `hy.json`. The three files have the same keys, and a test fails when a key is missing from one of them. To change a text, change it in all three files.

## Check your work

Run these from the `docs` folder:

```bash
npm test
npm run build
```

`npm test` checks that every language file has every key, that every sidebar link exists in every language (as a real page or as a placeholder), and that the placeholders and the `hreflang` links are written correctly. `npm run build` also fails on a broken link.

`npm run dev` writes the placeholders once when it starts. If you add a translated page while it runs, restart it.
