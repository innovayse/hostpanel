/**
 * Guard: the portal paints with theme tokens. A raw Tailwind palette class (`bg-gray-50`, `text-white`), an arbitrary
 * colour (`bg-[#fff]`) or a hex / rgb / hsl literal in a source file ties the screen to one theme. Colours live in
 * `assets/css/ui.css`, `assets/css/templates/*.css` and `tailwind.config.ts`, and are used through the token utilities
 * (`bg-ui-surface`, `bg-ui-page`, `text-on-primary`, the brand `primary` and `secondary` ramps ...).
 *
 * The portal was built on the stock greys with a `dark:` partner on every class, so the screens are not all moved yet:
 * `rawColourDebt.ts` lists the files that still carry a literal. The list is a ratchet. A file that is not on it must be
 * clean, and a file on it that no longer holds a literal must leave it (the test fails on a stale entry), so the list can
 * only shrink. `ALLOWED` is the permanent exception: files whose literal is data or arithmetic, never a surface. The
 * admin has the same guard in `admin/tests/lint/noRawColours.test.ts` (without a debt list: it is fully moved); the two
 * apps cannot read each other's files, so each reads its own.
 *
 * @module tests/config/noRawColours.test
 */

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve, sep } from 'node:path'
import { describe, expect, it } from 'vitest'
import { RAW_COLOUR_DEBT } from './rawColourDebt'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The folders and root files the guard reads; the stylesheets and `tailwind.config.ts` are the token files and are not read. */
const SOURCES: readonly string[] = [
  'components', 'composables', 'constants', 'layouts', 'lib', 'pages', 'plugins', 'server', 'stores', 'templates', 'types', 'utils',
  'app.vue', 'error.vue',
]

/** The names of the default Tailwind palette, `white` and `black` included. */
const PALETTE = 'white|black|slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose'

/** The utilities that paint a colour. */
const PAINTERS = 'text|bg|border(?:-[xytblrse])?|ring(?:-offset)?|from|via|to|fill|stroke|divide|outline|shadow|decoration|placeholder|caret|accent'

/** A raw palette class, with or without a variant, a shade or an opacity (`dark:bg-gray-900/50`, `text-white`). */
const PALETTE_CLASS = new RegExp(`(?<![\\w-])(?:${PAINTERS})-(?:${PALETTE})(?:-\\d{2,3})?(?![\\w-])`)

/** An arbitrary colour value (`bg-[#fff]`, `ring-[color:var(--x)]`). */
const ARBITRARY_COLOUR = new RegExp(`(?<![\\w-])(?:${PAINTERS})-\\[(?:#|rgb|hsl|oklch|oklab|color:)`)

/** A hex colour literal (`#fff`, `#3B82F6`, `#3B82F680`); a slot shorthand such as `#default` has non-hex letters and never matches. */
const HEX_COLOUR = /#[0-9a-fA-F]{3,8}(?![\w-])/

/** A colour function literal. */
const COLOUR_FUNCTION = /(?<![\w-])(?:rgba?|hsla?|oklch|oklab|lab|lch|hwb)\(/

/** Files that may hold a literal for good, relative to the client root, each with the reason: data or arithmetic, never a surface. */
const ALLOWED: Record<string, string> = {
  'composables/usePortalApps.ts': 'the brand gradient of each app tile in the app switcher (data the operator does not edit)',
  'plugins/color-mode.ts': 'the browser chrome colour per template and mode, set before the stylesheet applies',
  'server/routes/site.webmanifest.ts': 'the theme and background colour of the web app manifest, which cannot read a stylesheet',
  'utils/brandPalette.ts': 'colour arithmetic (WCAG contrast and the OKLCH ramp), a generated copy shared with the admin',
  'utils/brandingIcons.ts': 'the mask icon colour of a pinned tab, which a link tag carries as a literal',
  'utils/catalogGroups.ts': 'the series colours of the product families (data painted on a chart and a swatch)',
}

/**
 * Removes block, HTML and whole-line or trailing `//` comments, so only code is scanned.
 *
 * @param source File text.
 * @returns The text without comments.
 */
const stripComments = (source: string): string => source
  .replace(/\/\*[\s\S]*?\*\//g, '')
  .replace(/<!--[\s\S]*?-->/g, '')
  .replace(/(^|\s)\/\/.*$/gm, '$1')

/**
 * Lists every `.vue` and `.ts` file at a path.
 *
 * @param path Absolute file or folder to walk.
 * @returns Absolute paths of the files found; empty when the path does not exist.
 */
const sourceFiles = (path: string): string[] => {
  if (!existsSync(path)) {
    return []
  }
  if (statSync(path).isDirectory()) {
    return readdirSync(path).flatMap((name) => sourceFiles(join(path, name)))
  }
  return /\.(vue|ts)$/.test(path) ? [path] : []
}

/**
 * Finds the first literal colour in a text.
 *
 * @param code Comment-free source text.
 * @returns The offending text, or `undefined` when the text paints with tokens only.
 */
const findLiteralColour = (code: string): string | undefined => {
  for (const pattern of [PALETTE_CLASS, ARBITRARY_COLOUR, HEX_COLOUR, COLOUR_FUNCTION]) {
    const hit = pattern.exec(code)
    if (hit) {
      return hit[0]
    }
  }
  return undefined
}

/** Relative path and literal (or `undefined`) of every source file that is not on the permanent allow-list. */
const scanned: Array<[string, string | undefined]> = SOURCES
  .flatMap((entry) => sourceFiles(join(CLIENT_ROOT, entry)))
  .map((path) => relative(CLIENT_ROOT, path).split(sep).join('/'))
  .filter((name) => ALLOWED[name] === undefined)
  .map((name): [string, string | undefined] => [name, findLiteralColour(stripComments(readFileSync(join(CLIENT_ROOT, name), 'utf8')))])

/** The files of the debt list. */
const debt = new Set<string>(RAW_COLOUR_DEBT)

describe('no raw colours', () => {
  it('finds the sources', () => {
    expect(scanned.length).toBeGreaterThan(300)
  })

  it('lists only files that exist, with a reason for every allowed one', () => {
    for (const name of [...Object.keys(ALLOWED), ...RAW_COLOUR_DEBT]) {
      expect(existsSync(join(CLIENT_ROOT, name)), `${name} is listed but does not exist`).toBe(true)
    }
    for (const [name, reason] of Object.entries(ALLOWED)) {
      expect(reason.length, name).toBeGreaterThan(10)
    }
  })

  it('keeps the debt list sorted and free of allowed files', () => {
    expect([...RAW_COLOUR_DEBT]).toEqual([...RAW_COLOUR_DEBT].sort())
    for (const name of RAW_COLOUR_DEBT) {
      expect(ALLOWED[name], `${name} is both allowed and in debt`).toBeUndefined()
    }
  })

  it.each([
    ['a palette class', 'class="bg-red-600 p-2"'],
    ['a palette class with a variant and an opacity', 'class="dark:hover:bg-red-600/20"'],
    ['white text', 'class="text-white/60"'],
    ['an arbitrary hex colour', 'class="bg-[#fff]"'],
    ['an arbitrary colour variable', 'class="ring-[color:var(--x)]"'],
    ['a hex literal', "const fill = '#3B82F6'"],
    ['a colour function', "const edge = 'rgba(0, 0, 0, 0.5)'"],
  ])('detects %s', (_name, code) => {
    expect(findLiteralColour(code)).toBeDefined()
  })

  it.each([
    ['a kit token', 'class="bg-ui-surface text-on-primary border-ui-popover"'],
    ['the brand ramp', 'class="bg-primary-600 ring-primary-500/40 text-secondary-700"'],
    ['a template token', 'class="bg-classic-surface text-classic-ink"'],
    ['a slot shorthand', '<template #default>x</template>'],
    ['a tone without a palette name', 'class="bg-transparent text-current"'],
  ])('lets %s through', (_name, code) => {
    expect(findLiteralColour(code)).toBeUndefined()
  })

  it.each(scanned.filter(([name]) => !debt.has(name)))('%s paints with theme tokens only', (_name, literal) => {
    expect(literal).toBeUndefined()
  })

  it.each(scanned.filter(([name]) => debt.has(name)))('%s still holds a literal, so it belongs on the debt list', (_name, literal) => {
    expect(literal, 'the file is clean now: remove it from rawColourDebt.ts').toBeDefined()
  })
})
