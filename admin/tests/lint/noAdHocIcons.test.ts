/**
 * Guard: every icon of the admin comes from the one icon source, drawn by the shared icon component.
 *
 * Icons are stored shapes in `src/lib/icons.ts`, drawn by `UiIcon`, and a common action takes its icon from the
 * `actionIcons` map (`UiActionKind`). The test reads every `.vue` and `.ts` file under `src` and fails with the file and
 * the rule when a file:
 * - draws an inline `<svg>` outside the kit, where the drawn graphics (charts, swatches, avatars, the map) live;
 * - holds icon shape markup in a string outside `lib/icons.ts`;
 * - uses a text character as an icon: a check, a cross, an arrow, a bullet, a triangle, a star, an emoji;
 * - imports an icon package or an `.svg` file outside the kit (`src/components/ui`).
 *
 * The locale files are scanned for emoji too. A few kit files draw graphics rather than icons; each is named in
 * {@link SVG_ALLOWED} with the reason, and the list is checked: an entry whose file no longer draws an `<svg>` fails, so
 * it cannot rot. The client has the same guard in `client/tests/config/noAdHocIcons.test.ts`: the two apps cannot read
 * each other's files, so each reads its own.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** The kit folder, the one place an icon package may be imported. */
const KIT = 'components/ui/'

/** The one module that holds icon shapes. */
const ICON_SOURCE = 'lib/icons.ts'

/** The rules the guard enforces. */
enum IconRule {
  /** An `<svg>` drawn in a template outside the allowed kit graphics. */
  InlineSvg = 'inline-svg',
  /** Icon shape markup in a string outside the icon source. */
  ShapeMarkup = 'shape-markup',
  /** A text character used as an icon. */
  GlyphIcon = 'glyph-icon',
  /** An icon package or `.svg` file imported outside the kit. */
  IconImport = 'icon-import',
}

/** One kit file allowed to draw an `<svg>`, with the reason. */
interface SvgException {
  /** Path under `src`, with forward slashes. */
  file: string
  /** Why the file draws a graphic rather than an icon. */
  reason: string
}

/** The kit files that draw graphics, not icons. */
const SVG_ALLOWED: readonly SvgException[] = [
  { file: 'components/ui/UiIcon.vue', reason: 'the shared icon component itself' },
  { file: 'components/ui/UiChartGauge.vue', reason: 'chart: a gauge arc' },
  { file: 'components/ui/UiChartSkeleton.vue', reason: 'chart placeholder shapes' },
  { file: 'components/ui/UiColorDot.vue', reason: 'a colour dot, a swatch not an icon' },
  { file: 'components/ui/UiColorSwatch.vue', reason: 'a colour swatch, not an icon' },
  { file: 'components/ui/UiPluginAvatar.vue', reason: 'a lettered avatar drawn as text on a square' },
  { file: 'components/ui/UiSvgMap.vue', reason: 'a data map, not an icon' },
]

/** Text characters that read as icons: dingbats, arrows, bullets, geometric shapes, the multiplication sign, the vertical ellipsis, emoji. */
const GLYPH = /[←-⇿•‣⋮⋯■-◿☀-➿⬀-⯿×\u{1F000}-\u{1FAFF}]|\uFE0F/u

/** Emoji only, the range the locale files are held to. */
const EMOJI = /[☀-➿⬀-⯿\u{1F000}-\u{1FAFF}]|\uFE0F/u

/** An opening `<svg` tag. */
const SVG_TAG = /<svg[\s>]/

/** A string that starts with a shape tag: icon markup. */
const SHAPE_STRING = /['"`]<(?:path|circle|polyline|polygon|line|rect|ellipse)\b/

/** An import of an icon package or an `.svg` file. */
const ICON_IMPORT = /from\s+['"](?:@iconify\/[^'"]*|lucide[^'"]*|@heroicons\/[^'"]*|@fortawesome\/[^'"]*|@tabler\/[^'"]*|[^'"]*\.svg(?:\?[^'"]*)?)['"]/

/**
 * Lists the files of a folder with a given extension, walking down.
 *
 * @param dir - Absolute folder to walk.
 * @param extensions - File name endings to keep.
 * @returns Absolute paths found.
 */
const filesUnder = (dir: string, extensions: readonly string[]): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return filesUnder(path, extensions)
    }
    return extensions.some((extension) => name.endsWith(extension)) ? [path] : []
  })

/**
 * Removes comments, so a character or a tag that is only described is not counted.
 *
 * @param source - File contents.
 * @returns The contents without HTML, block and line comments.
 */
const withoutComments = (source: string): string =>
  source
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:'"`])\/\/[^\n]*/g, '$1')

/**
 * Finds which rules a source file breaks.
 *
 * @param file - Path under `src`, with forward slashes.
 * @param source - File contents.
 * @returns The broken rules, each once, in rule order.
 */
const brokenRules = (file: string, source: string): IconRule[] => {
  const code = withoutComments(source)
  const broken = new Set<IconRule>()
  if (SVG_TAG.test(code) && !SVG_ALLOWED.some((entry) => entry.file === file)) {
    broken.add(IconRule.InlineSvg)
  }
  if (file !== ICON_SOURCE && SHAPE_STRING.test(code)) {
    broken.add(IconRule.ShapeMarkup)
  }
  if (GLYPH.test(code)) {
    broken.add(IconRule.GlyphIcon)
  }
  if (!file.startsWith(KIT) && ICON_IMPORT.test(code)) {
    broken.add(IconRule.IconImport)
  }
  return Object.values(IconRule).filter((rule) => broken.has(rule))
}

/** Every source file as its path under `src` and its contents. */
const SOURCES: ReadonlyArray<readonly [string, string]> = filesUnder(SRC, ['.vue', '.ts']).map(
  (path) => [relative(SRC, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

/** Every locale file as its path under `src` and its contents. */
const LOCALES: ReadonlyArray<readonly [string, string]> = filesUnder(join(SRC, 'locales'), ['.json']).map(
  (path) => [relative(SRC, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

describe('ad hoc icons', () => {
  it('reads the screens, the kit and the icon source', () => {
    const files = SOURCES.map(([file]) => file)

    expect(files).toContain('views/ServersView.vue')
    expect(files).toContain('components/ui/UiIcon.vue')
    expect(files).toContain(ICON_SOURCE)
    expect(LOCALES.length).toBeGreaterThan(0)
  })

  it.each(SOURCES)('%s draws its icons through the shared icon source', (file, source) => {
    expect(brokenRules(file, source)).toEqual([])
  })

  it.each(LOCALES)('%s holds no emoji', (_file, source) => {
    expect(EMOJI.test(source)).toBe(false)
  })

  it.each(SVG_ALLOWED.map((entry) => [entry.file, entry.reason] as const))('%s still draws an svg (%s)', (file) => {
    const source = SOURCES.find(([path]) => path === file)?.[1]

    expect(source).toBeDefined()
    expect(SVG_TAG.test(withoutComments(source ?? ''))).toBe(true)
  })

  describe('self test', () => {
    it('catches an inline svg outside the allowed graphics', () => {
      expect(brokenRules('views/AView.vue', '<template><svg viewBox="0 0 24 24"><path d="M1 1" /></svg></template>')).toEqual([IconRule.InlineSvg])
      expect(brokenRules('components/ui/UiIcon.vue', '<template><svg /></template>')).toEqual([])
    })

    it('catches icon shapes held in a string outside the icon source', () => {
      expect(brokenRules('views/AView.vue', "const X = '<line x1=\"1\" y1=\"1\" x2=\"2\" y2=\"2\"/>'")).toEqual([IconRule.ShapeMarkup])
      expect(brokenRules(ICON_SOURCE, "Close: '<line x1=\"1\" y1=\"1\" x2=\"2\" y2=\"2\"/>'")).toEqual([])
    })

    it('catches a text character used as an icon', () => {
      for (const glyph of ['✓', '✕', '×', '→', '▲', '•', '⭐', '\u{1F514}']) {
        expect(brokenRules('views/AView.vue', `<template><span>${glyph}</span></template>`)).toEqual([IconRule.GlyphIcon])
      }
    })

    it('lets plain punctuation and described characters through', () => {
      expect(brokenRules('views/AView.vue', '<template><span>a · b …</span></template>')).toEqual([])
      expect(brokenRules('views/AView.vue', '// the ✓ is drawn by the kit\nconst x = 1')).toEqual([])
      expect(brokenRules('views/AView.vue', '/** The × clears. */\nconst x = 1')).toEqual([])
    })

    it('catches an icon package or an svg file imported outside the kit', () => {
      expect(brokenRules('views/AView.vue', "import { Icon } from '@iconify/vue'")).toEqual([IconRule.IconImport])
      expect(brokenRules('views/AView.vue', "import { Check } from 'lucide-vue-next'")).toEqual([IconRule.IconImport])
      expect(brokenRules('views/AView.vue', "import logo from '@/assets/logo.svg'")).toEqual([IconRule.IconImport])
      expect(brokenRules('components/ui/UiNamedIcon.vue', "import { Icon } from '@iconify/vue'")).toEqual([])
    })
  })
})
