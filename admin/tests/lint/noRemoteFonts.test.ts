/**
 * Guard: no font is loaded from another host at runtime. Every face the admin and the client use
 * ships inside the project (an npm font package or a committed file) and is served from the
 * panel's own origin. This test reads the admin page, the client head config and every CSS entry
 * point, and fails as soon as a font URL pointing at another host comes back.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

/** Repository root: the admin and client folders sit side by side beneath it. */
const REPO = join(__dirname, '..', '..', '..')

/** Patterns that mean a font is fetched from another host. */
const REMOTE_FONT_PATTERNS: readonly RegExp[] = [
  // Known third-party font services, under any scheme.
  /fonts\.googleapis\.com|fonts\.gstatic\.com|use\.typekit\.net|fonts\.bunny\.net|fast\.fonts\.net/i,
  // An absolute URL to a font file.
  /(?:https?:)?\/\/[^\s'")]+\.(?:woff2?|ttf|otf|eot)\b/i,
  // A stylesheet pulled in by absolute URL from CSS.
  /@import\s+(?:url\()?\s*['"]?(?:https?:)?\/\//i,
  // A preconnect hint to another host, only ever added for remote fonts here.
  /rel=["']?preconnect/i,
]

/**
 * Lists every `.css` file under a folder, skipping build output and dependencies.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the stylesheets found.
 */
const cssFiles = (dir: string): string[] => {
  if (!existsSync(dir)) {
    return []
  }
  return readdirSync(dir).flatMap((name) => {
    if (name === 'node_modules' || name === 'dist' || name.startsWith('.')) {
      return []
    }
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return cssFiles(path)
    }
    return name.endsWith('.css') ? [path] : []
  })
}

/** Files the browser reads to decide which fonts to fetch. */
const SOURCES: readonly string[] = [
  join(REPO, 'admin', 'index.html'),
  join(REPO, 'client', 'nuxt.config.ts'),
  ...cssFiles(join(REPO, 'admin', 'src')),
  ...cssFiles(join(REPO, 'client', 'assets')),
]

/**
 * Finds the remote font references in one file.
 *
 * @param text - File contents.
 * @returns The matched snippets, empty when the file is clean.
 */
const remoteFonts = (text: string): string[] =>
  REMOTE_FONT_PATTERNS.flatMap((pattern) => {
    const match = text.match(pattern)
    return match ? [match[0]] : []
  })

describe('no remote fonts', () => {
  it('reads the admin page, the client head config and the CSS entry points', () => {
    expect(SOURCES.every((path) => existsSync(path))).toBe(true)
    expect(SOURCES.some((path) => path.endsWith('main.css'))).toBe(true)
  })

  it.each(SOURCES.map((path) => [relative(REPO, path), path]))('%s loads no font from another host', (_name, path) => {
    expect(remoteFonts(readFileSync(path, 'utf8'))).toEqual([])
  })

  it('catches a font stylesheet link coming back', () => {
    expect(remoteFonts('<link href="https://fonts.googleapis.com/css2?family=X" rel="stylesheet">')).not.toEqual([])
    expect(remoteFonts('@font-face { src: url(https://cdn.example.com/x.woff2) }')).not.toEqual([])
    expect(remoteFonts('@import url("https://cdn.example.com/fonts.css");')).not.toEqual([])
  })

  it('leaves bundled font imports alone', () => {
    expect(remoteFonts('@import "@fontsource-variable/inter";')).toEqual([])
  })
})
