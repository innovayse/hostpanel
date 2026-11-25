/**
 * Guard: every colour the admin paints comes from a theme token. A raw Tailwind palette class (`bg-red-600`,
 * `text-white`, `border-slate-200`), an arbitrary colour (`bg-[#fff]`) or a hex / rgb / hsl literal in a source
 * file ties the screen to one theme: it stays white on a surface that turns dark, or keeps a dark label on a
 * surface that turns light. Colours live in `src/assets/main.css` (both palettes, one token each) and are used
 * through the token utilities (`bg-surface-card`, `text-text-primary`, `text-status-red`, `bg-tint-red` ...).
 *
 * The test reads every `.vue`, `.ts` and `.css` file under `src`, with comments removed (a doc comment may
 * quote an example colour), and fails on the first literal colour outside the allow-list below. Each entry
 * of the allow-list is a file whose literal is data or arithmetic, never a surface, and says why.
 */
import { readdirSync, readFileSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The admin source folder. */
const SRC_ROOT = join(__dirname, '..', '..', 'src')

/** File extensions that can carry a colour. */
const SCANNED = /\.(vue|ts|css)$/

/** The names of the default Tailwind palette, `white` and `black` included. */
const PALETTE = 'white|black|slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose'

/** The utilities that paint a colour. */
const PAINTERS = 'text|bg|border(?:-[xytblrse])?|ring(?:-offset)?|from|via|to|fill|stroke|divide|outline|shadow|decoration|placeholder|caret|accent'

/** A raw palette class, with or without a variant, a shade or an opacity (`hover:bg-red-600/20`, `text-white`). */
const PALETTE_CLASS = new RegExp(`(?<![\\w-])(?:${PAINTERS})-(?:${PALETTE})(?:-\\d{2,3})?(?![\\w-])`)

/** An arbitrary colour value (`bg-[#fff]`, `text-[rgb(0_0_0)]`). */
const ARBITRARY_COLOUR = new RegExp(`(?<![\\w-])(?:${PAINTERS})-\\[(?:#|rgb|hsl|oklch|oklab|color:)`)

/** A hex colour literal (`#fff`, `#3B82F6`, `#3B82F680`); a slot shorthand such as `#default` has non-hex letters and never matches. */
const HEX_COLOUR = /#[0-9a-fA-F]{3,8}(?![\w-])/

/** A colour function literal. */
const COLOUR_FUNCTION = /(?<![\w-])(?:rgba?|hsla?|oklch|oklab|lab|lch|hwb)\(/

/**
 * The files that may hold a literal colour, relative to `src` with forward slashes, each with the reason.
 * A new entry needs a reason that is data or arithmetic; a surface or a text colour is a token instead.
 */
const ALLOWED: Record<string, string> = {
  'assets/main.css': 'the theme token file: both palettes and every token are declared here',
  'lib/brandPalette.ts': 'colour arithmetic (WCAG contrast and the OKLCH ramp), a generated copy shared with the portal',
  'lib/themeBoot.ts': 'the browser chrome colour painted before the stylesheet loads, pinned to the ground tokens by tests/assets/mainCss.test.ts',
  'utils/ui/chart.ts': 'the canvas palette a chart uses when the stylesheet is not loaded, pinned to the chart tokens by tests/assets/mainCss.test.ts',
  'utils/ui/color.ts': 'the colour helpers of a run-time colour (a plugin avatar): the neutral block and the two label colours',
  'components/clients/ClientGroupFormDialog.vue': 'the default of the colour an operator picks for a client group (data)',
  'components/settings/BrandColorsCard.vue': 'the default brand colours and the dark label used to test contrast (operator data)',
  'views/SlideFormView.vue': 'the default of the colour an operator picks for a slide (data)',
  'types/integrationmeta.ts': 'the vendor tile colour behind a white integration logo, the same in every theme',
}

/**
 * Removes block, HTML and whole-line or trailing `//` comments, so only code is scanned.
 *
 * @param source - File text.
 * @returns The text without comments.
 */
const stripComments = (source: string): string => source
  .replace(/\/\*[\s\S]*?\*\//g, '')
  .replace(/<!--[\s\S]*?-->/g, '')
  .replace(/(^|\s)\/\/.*$/gm, '$1')

/**
 * Lists every scanned file under a folder.
 *
 * @param folder - Absolute folder path.
 * @returns Absolute paths of the files that can carry a colour.
 */
const filesUnder = (folder: string): string[] => readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
  const path = join(folder, entry.name)
  if (entry.isDirectory()) {
    return filesUnder(path)
  }
  return SCANNED.test(entry.name) ? [path] : []
})

/**
 * Finds the first literal colour in a text.
 *
 * @param code - Comment-free source text.
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

/** Path (relative to `src`) and comment-free text of every scanned file that is not on the allow-list. */
const sources: Array<[string, string]> = filesUnder(SRC_ROOT)
  .map((path): [string, string] => [relative(SRC_ROOT, path).split(sep).join('/'), path])
  .filter(([name]) => ALLOWED[name] === undefined)
  .map(([name, path]): [string, string] => [name, stripComments(readFileSync(path, 'utf8'))])

describe('no raw colours', () => {
  it('finds the sources', () => {
    expect(sources.length).toBeGreaterThan(300)
  })

  it('allows only files that exist', () => {
    const present = new Set(filesUnder(SRC_ROOT).map((path) => relative(SRC_ROOT, path).split(sep).join('/')))
    for (const name of Object.keys(ALLOWED)) {
      expect(present.has(name), `${name} is on the allow-list but does not exist`).toBe(true)
    }
  })

  it('gives every allowed file a reason', () => {
    for (const [name, reason] of Object.entries(ALLOWED)) {
      expect(reason.length, name).toBeGreaterThan(10)
    }
  })

  it.each([
    ['a palette class', 'class="bg-red-600 p-2"'],
    ['a palette class with a variant and an opacity', 'class="hover:bg-red-600/20"'],
    ['white text', 'class="text-white/60"'],
    ['a black backdrop', 'class="bg-black/60"'],
    ['an arbitrary hex colour', 'class="bg-[#fff]"'],
    ['a hex literal', "const fill = '#3B82F6'"],
    ['a colour function', "const edge = 'rgb(0 0 0 / 0.5)'"],
  ])('detects %s', (_name, code) => {
    expect(findLiteralColour(code)).toBeDefined()
  })

  it.each([
    ['a surface token', 'class="bg-surface-card text-text-primary border-border"'],
    ['a status token', 'class="text-status-red bg-tint-red border-status-red/30"'],
    ['the brand ramp', 'class="bg-primary-500 text-text-on-primary ring-primary-500/40"'],
    ['a variable reference', 'class="bg-(--overlay-backdrop) ring-(--text-accent)"'],
    ['a slot shorthand', '<template #default>x</template>'],
    ['a tone without a palette name', 'class="bg-transparent text-current"'],
  ])('lets %s through', (_name, code) => {
    expect(findLiteralColour(code)).toBeUndefined()
  })

  it.each(sources)('%s paints with theme tokens only', (_name, code) => {
    expect(findLiteralColour(code)).toBeUndefined()
  })
})
