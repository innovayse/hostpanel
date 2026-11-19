/**
 * Guard: nothing in the admin is given a fixed width wider than a small phone without a responsive prefix.
 *
 * A bare `w-[900px]`, `min-w-[24rem]` or `w-96` in a class list is 900, 384 or 384 pixels on every screen, so on a
 * 360 px phone it pushes the page sideways. Under `src` the guard reads every `.vue` and `.ts` file (comments
 * stripped) and fails on a `w-` or `min-w-` utility wider than 320 px (the smallest phone the admin supports) that
 * carries no variant prefix (`sm:`, `md:`, `max-sm:` and the like). `max-w-` never forces a width and is not read.
 * The fix is a `max-w-full`-style clamp, a one-column base with a wider step from `sm:` up, or a narrower value.
 *
 * A few places are allowed, each named below with the reason. The list is checked too: an entry whose file is gone
 * or no longer holds its width fails, so the list cannot rot.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** Widest fixed width, in pixels, a class may give without a responsive prefix. */
const MAX_FIXED_WIDTH_PX = 320

/** Pixels in one rem. */
const PX_PER_REM = 16

/** Pixels in one step of the default spacing scale (`w-96` is 96 steps of a quarter rem). */
const PX_PER_SCALE_STEP = 4

/** Container sizes the named width utilities stand for, in pixels (`w-3xs` to `w-7xl`). */
const NAMED_WIDTH_PX: Readonly<Record<string, number>> = {
  '3xs': 256,
  '2xs': 288,
  xs: 320,
  sm: 384,
  md: 448,
  lg: 512,
  xl: 576,
  '2xl': 672,
  '3xl': 768,
  '4xl': 896,
  '5xl': 1024,
  '6xl': 1152,
  '7xl': 1280,
}

/**
 * A `w-` or `min-w-` utility with no variant prefix: not preceded by a word character, a dash or a colon, so
 * `max-w-`, `sm:w-` and `max-sm:min-w-` never match. The value is an arbitrary pixel or rem width, a scale step or a name.
 */
const FIXED_WIDTH = /(?<![\w:-])(?:min-w|w)-(?:\[(\d+(?:\.\d+)?)(px|rem)\]|(\d+(?:\.\d+)?)(?![\w.[-])|(3xs|2xs|xs|sm|md|lg|xl|2xl|3xl|4xl|5xl|6xl|7xl)(?![\w-]))/g

/** One file allowed to hold one wide fixed width, with the reason it may. */
interface AllowedWidth {
  /** Path under `src`, with forward slashes. */
  file: string
  /** The class as written. */
  utility: string
  /** Why the width cannot overflow a phone. */
  reason: string
}

/** The named exceptions: a floating popup that is clamped to the screen by its own `max-w`. */
const ALLOWED: readonly AllowedWidth[] = [
  {
    file: 'components/ui/UiDateRangePicker.vue',
    utility: 'w-[900px]',
    reason: 'the two-month popup is clamped with max-w-[calc(100vw-32px)] and placed inside the screen by useFloatingPlace',
  },
]

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
 * Lists every `.vue` and `.ts` file under a folder.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the files found.
 */
const sourceFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return sourceFiles(path)
    }
    return name.endsWith('.vue') || name.endsWith('.ts') ? [path] : []
  })

/**
 * Finds the unprefixed fixed widths wider than the limit in a piece of source.
 *
 * @param source - File text; comments are stripped first.
 * @returns Each offending utility as written, in order of appearance.
 */
const wideFixedWidths = (source: string): string[] =>
  [...stripComments(source).matchAll(FIXED_WIDTH)].flatMap((match) => {
    const [utility, arbitrary, unit, step, name] = match
    let pixels = 0
    if (arbitrary !== undefined) {
      pixels = Number(arbitrary) * (unit === 'rem' ? PX_PER_REM : 1)
    } else if (step !== undefined) {
      pixels = Number(step) * PX_PER_SCALE_STEP
    } else if (name !== undefined) {
      pixels = NAMED_WIDTH_PX[name] ?? 0
    }
    return pixels > MAX_FIXED_WIDTH_PX ? [utility] : []
  })

/** Path under `src` and offences of every file that has any. */
const offences: Array<[string, string[]]> = sourceFiles(SRC)
  .map((path): [string, string[]] => [relative(SRC, path).split(sep).join('/'), wideFixedWidths(readFileSync(path, 'utf8'))])
  .filter(([, found]) => found.length > 0)

describe('fixed widths', () => {
  it('finds a wide pixel, rem, scale or named width', () => {
    expect(wideFixedWidths('class="w-[900px] min-w-[24rem] w-96 min-w-sm"')).toEqual(['w-[900px]', 'min-w-[24rem]', 'w-96', 'min-w-sm'])
  })

  it('lets a width of at most 320 px through', () => {
    expect(wideFixedWidths('class="w-[320px] w-80 w-72 min-w-[12rem] min-w-xs w-64 w-full"')).toEqual([])
  })

  it('lets a prefixed width through, whatever the variant', () => {
    expect(wideFixedWidths('class="sm:w-[900px] md:min-w-[24rem] max-md:w-96 lg:w-3xl"')).toEqual([])
  })

  it('ignores max-w, which never forces a width', () => {
    expect(wideFixedWidths('class="max-w-[900px] max-w-4xl"')).toEqual([])
  })

  it('ignores a width quoted in a comment', () => {
    expect(wideFixedWidths('/** the class is `w-[900px]` */\n// w-96\n<!-- w-[500px] -->')).toEqual([])
  })

  it('finds the source folder', () => {
    expect(sourceFiles(SRC).length).toBeGreaterThan(100)
  })

  it('allows only the named exceptions', () => {
    const allowed = new Set(ALLOWED.map((entry) => `${entry.file} ${entry.utility}`))
    const unexpected = offences.flatMap(([file, found]) => found.filter((utility) => !allowed.has(`${file} ${utility}`)).map((utility) => `${file}: ${utility}`))

    expect(unexpected).toEqual([])
  })

  it('keeps no exception that no longer holds', () => {
    const stale = ALLOWED.filter((entry) => !offences.some(([file, found]) => file === entry.file && found.includes(entry.utility)))

    expect(stale).toEqual([])
  })
})
