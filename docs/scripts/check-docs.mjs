/**
 * Post-build checks that VitePress's own link checker does not cover:
 *
 * 1. Missing images: every <img src="..."> in the built HTML must resolve to a
 *    file that exists in the dist directory.
 * 2. Orphan pages: every .md source file (except index files that are covered
 *    by their directory URL) must appear at least once in the sidebar defined in
 *    config.ts, so a new page cannot be published without a navigation entry.
 *
 * Exit code 0 means all checks passed.  Exit code 1 means at least one failure
 * was found; all failures are printed before exiting so a single run shows the
 * full list.
 */

import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs'
import { join, relative, extname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { listMarkdown, listEnglishPages, readSettings } from './lib/localeFallbacks.mjs'

const __dirname = fileURLToPath(new URL('.', import.meta.url))
const docsRoot = join(__dirname, '..')
const distDir = join(docsRoot, '.vitepress', 'dist')

/**
 * URL base the site was built with, same rule as config.ts: leading and trailing slash,
 * default /docs/. Image paths in the built HTML start with it.
 */
const base = `/${(process.env.DOCS_BASE?.trim() || '/docs/').replace(/^\/+|\/+$/g, '')}/`.replace(/^\/\/$/, '/')

let failures = 0

// ---------------------------------------------------------------------------
// 1. Missing images
// ---------------------------------------------------------------------------

/**
 * Recursively collects every .html file path under a directory.
 * @param {string} dir
 * @returns {string[]}
 */
const collectHtml = (dir) => {
  const entries = readdirSync(dir, { withFileTypes: true })
  return entries.flatMap((e) =>
    e.isDirectory()
      ? collectHtml(join(dir, e.name))
      : e.name.endsWith('.html')
        ? [join(dir, e.name)]
        : [],
  )
}

/** @type {RegExp} Matches src="..." attributes in HTML. */
const imgSrcRe = /<img[^>]+src="([^"]+)"/g

for (const htmlFile of collectHtml(distDir)) {
  const html = readFileSync(htmlFile, 'utf8')
  let m
  while ((m = imgSrcRe.exec(html)) !== null) {
    const src = m[1]
    // Skip external URLs and data URIs.
    if (src.startsWith('http') || src.startsWith('data:')) { continue }
    // A root-relative address must sit under the base: anything else works in a dev server but is a 404 once
    // the site is served from its base path.
    if (src.startsWith('/') && !src.startsWith('//') && !src.startsWith(base)) {
      console.error(`Image outside the base ${base}: ${src}  (referenced in ${relative(distDir, htmlFile)})`)
      failures++
      continue
    }
    // Strip query/hash, strip leading slash.
    const clean = src.split('?')[0].split('#')[0].replace(base.length > 1 ? new RegExp(`^${base}`) : /^\//, '')
    const abs = join(distDir, clean)
    try {
      statSync(abs)
    } catch {
      console.error(`Missing image: ${src}  (referenced in ${relative(distDir, htmlFile)})`)
      failures++
    }
  }
}

// ---------------------------------------------------------------------------
// 2. Orphan pages: .md files not reachable from the sidebar
// ---------------------------------------------------------------------------

/**
 * Reads the sidebar links from `.vitepress/i18n/sidebar.json`, the one list the config builds the sidebar of
 * every language from. This avoids importing the TS config at runtime.
 * @returns {Set<string>} Normalised link paths without trailing slash.
 */
const sidebarLinks = () => {
  const sidebar = JSON.parse(readFileSync(join(docsRoot, '.vitepress', 'i18n', 'sidebar.json'), 'utf8'))
  const links = new Set()
  for (const link of [...sidebar.main.flatMap((section) => section.links), ...sidebar.apiReference.links]) {
    links.add(link.replace(/\/$/, ''))
  }
  return links
}

/** The language settings: the folders of the languages other than English are not part of the English tree. */
const localeSettings = readSettings()

/** Source folders of the languages other than English. */
const localePrefixes = Object.entries(localeSettings.locales)
  .filter(([key]) => key !== localeSettings.default)
  .map(([, locale]) => locale.folder)

/**
 * Recursively collects every .md source file under a directory, returning
 * their VitePress-style paths (e.g. /operators/install).
 * @param {string} dir
 * @param {string} base
 * @returns {string[]}
 */
const collectMd = (dir, base = '') => {
  const entries = readdirSync(dir, { withFileTypes: true })
  return entries.flatMap((e) => {
    // Skip directories that are not user-facing docs pages (generated partials start with _).
    if (e.isDirectory() && (e.name === 'brand' || e.name === 'node_modules' || e.name.startsWith('.') || e.name.startsWith('_'))) { return [] }
    if (e.isDirectory()) { return collectMd(join(dir, e.name), `${base}/${e.name}`) }
    if (extname(e.name) !== '.md') { return [] }
    const stem = e.name.replace(/\.md$/, '')
    // index.md → the directory URL itself
    return [`${base}${stem === 'index' ? '' : '/' + stem}`]
  })
}

const links = sidebarLinks()

/**
 * Adds every link of a sidebar item and, recursively, of the groups inside it.
 * @param {{ link?: string, items?: object[] }} item
 */
const addSidebarLinks = (item) => {
  if (item.link) { links.add(item.link.replace(/\/$/, '')) }
  for (const child of item.items ?? []) { addSidebarLinks(child) }
}

// Also collect links from generated-sidebar.json when present (API reference endpoint pages,
// grouped by audience and then by area).
const generatedSidebarPath = join(docsRoot, 'en', 'api-reference', 'generated-sidebar.json')
if (existsSync(generatedSidebarPath)) {
  for (const group of JSON.parse(readFileSync(generatedSidebarPath, 'utf8'))) { addSidebarLinks(group) }
}

// Pages intentionally excluded from the sidebar (home page, components demo, and the changelog, which the top bar links to).
const excluded = new Set(['', '/components', '/changelog'])

for (const page of collectMd(join(docsRoot, 'en'))) {
  if (excluded.has(page)) { continue }
  if (!links.has(page)) {
    console.error(`Page not in sidebar: ${page}.md`)
    failures++
  }
}

// ---------------------------------------------------------------------------
// 3. Translations without an English page
// ---------------------------------------------------------------------------

// A translated page needs the English page it translates; without it nothing links to the translation.
const englishPages = new Set(listEnglishPages(docsRoot, localeSettings))
for (const prefix of localePrefixes) {
  for (const page of listMarkdown(join(docsRoot, prefix))) {
    if (!englishPages.has(page)) {
      console.error(`Translation without an English page: ${prefix}/${page}`)
      failures++
    }
  }
}

if (failures > 0) {
  console.error(`\n${failures} check(s) failed.`)
  process.exit(1)
} else {
  console.log('All docs checks passed.')
}
