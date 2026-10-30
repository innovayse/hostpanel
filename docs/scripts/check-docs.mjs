/**
 * Post-build checks that VitePress's own link checker does not cover:
 *
 * 1. Missing images — every <img src="..."> in the built HTML must resolve to a
 *    file that exists in the dist directory.
 * 2. Orphan pages — every .md source file (except index files that are covered
 *    by their directory URL) must appear at least once in the sidebar defined in
 *    config.ts, so a new page cannot be published without a navigation entry.
 *
 * Exit code 0 means all checks passed.  Exit code 1 means at least one failure
 * was found; all failures are printed before exiting so a single run shows the
 * full list.
 */

import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative, extname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = fileURLToPath(new URL('.', import.meta.url))
const docsRoot = join(__dirname, '..')
const distDir = join(docsRoot, '.vitepress', 'dist')

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
    if (src.startsWith('http') || src.startsWith('data:')) continue
    // Strip query/hash, strip leading slash.
    const clean = src.split('?')[0].split('#')[0].replace(/^\/hostpanel\//, '')
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
// 2. Orphan pages — .md files not reachable from the sidebar
// ---------------------------------------------------------------------------

/**
 * Reads the sidebar links from config.ts by matching on the link property.
 * This avoids importing the TS file at runtime.
 * @returns {Set<string>} Normalised link paths without trailing slash.
 */
const sidebarLinks = () => {
  const config = readFileSync(join(docsRoot, '.vitepress', 'config.ts'), 'utf8')
  const linkRe = /link:\s*'([^']+)'/g
  const links = new Set()
  let m
  while ((m = linkRe.exec(config)) !== null) {
    // Skip external links.
    if (m[1].startsWith('http')) continue
    links.add(m[1].replace(/\/$/, ''))
  }
  return links
}

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
    if (e.isDirectory()) return collectMd(join(dir, e.name), `${base}/${e.name}`)
    if (extname(e.name) !== '.md') return []
    const stem = e.name.replace(/\.md$/, '')
    // index.md → the directory URL itself
    return [`${base}${stem === 'index' ? '' : '/' + stem}`]
  })
}

const links = sidebarLinks()

// Pages intentionally excluded from the sidebar (home page, components demo).
const excluded = new Set(['', '/components'])

for (const page of collectMd(docsRoot)) {
  if (excluded.has(page)) continue
  if (!links.has(page)) {
    console.error(`Page not in sidebar: ${page}.md`)
    failures++
  }
}

if (failures > 0) {
  console.error(`\n${failures} check(s) failed.`)
  process.exit(1)
} else {
  console.log('All docs checks passed.')
}
