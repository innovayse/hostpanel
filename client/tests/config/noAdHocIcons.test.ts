/**
 * Guard: every icon of the client comes from the one icon library, drawn as a component of it.
 *
 * The client's icons are components of `lucide-vue-next`. The test reads every `.vue` and `.ts` file of the app (the
 * tests, the end-to-end specs and the build output left out) and fails with the file and the rule when a file:
 * - draws an inline `<svg>` outside the kit graphics named in {@link SVG_ALLOWED};
 * - holds icon shape markup in a string;
 * - uses a text character as an icon: a check, a cross, an arrow, a bullet, a triangle, a star, an emoji
 *   ({@link GLYPH_ALLOWED} names the few files that handle such characters as data);
 * - imports an icon package other than the library, or an `.svg` file.
 *
 * The locale files are scanned for emoji too. Both allow lists are checked: an entry whose file no longer breaks its
 * rule fails, so the lists cannot rot. The admin has the same guard in `admin/tests/lint/noAdHocIcons.test.ts`: the two
 * apps cannot read each other's files, so each reads its own.
 *
 * @module tests/config/noAdHocIcons.test
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The folders and root files the guard reads. */
const SOURCES: readonly string[] = [
  'components', 'pages', 'layouts', 'templates', 'constants', 'utils', 'lib', 'composables', 'stores', 'middleware',
  'plugins', 'types', 'server', 'app.vue', 'error.vue',
]

/** The rules the guard enforces. */
enum IconRule {
  /** An `<svg>` drawn in a template outside the allowed graphics. */
  InlineSvg = 'inline-svg',
  /** Icon shape markup in a string. */
  ShapeMarkup = 'shape-markup',
  /** A text character used as an icon. */
  GlyphIcon = 'glyph-icon',
  /** An icon package other than the library, or an `.svg` file, imported. */
  IconImport = 'icon-import',
}

/** One file named in an allow list, with the reason. */
interface AllowedFile {
  /** Path under the client root, with forward slashes. */
  file: string
  /** Why the file is not an ad hoc icon. */
  reason: string
}

/** The files that draw a graphic, not an icon. */
const SVG_ALLOWED: readonly AllowedFile[] = [
  { file: 'components/ui/UiProgress.vue', reason: 'a progress bar drawn as a rectangle, not an icon' },
]

/** The files that handle a glyph as data, not as an icon. */
const GLYPH_ALLOWED: readonly AllowedFile[] = [
  { file: 'utils/catalogGroups.ts', reason: 'reads the bullet an operator typed in a product description and turns it into a list' },
  { file: 'utils/ui/richText.ts', reason: 'the table of HTML entities the rich text decodes' },
  { file: 'components/account/StoredMethodsCard.vue', reason: 'the masked card number, four dots as text' },
]

/** Text characters that read as icons: dingbats, arrows, bullets, geometric shapes, the multiplication sign, the vertical ellipsis, emoji. */
const GLYPH = /[←-⇿•‣⋮⋯■-◿☀-➿⬀-⯿×️\u{1F000}-\u{1FAFF}]/u

/** Emoji only, the range the locale files are held to. */
const EMOJI = /[☀-➿⬀-⯿️\u{1F000}-\u{1FAFF}]/u

/** An opening `<svg` tag. */
const SVG_TAG = /<svg[\s>]/

/** A string that starts with a shape tag: icon markup. */
const SHAPE_STRING = /['"`]<(?:path|circle|polyline|polygon|line|rect|ellipse)\b/

/** An import of an icon package other than the library, or of an `.svg` file. */
const ICON_IMPORT = /from\s+['"](?:@iconify\/[^'"]*|@heroicons\/[^'"]*|@fortawesome\/[^'"]*|@tabler\/[^'"]*|lucide-(?!vue-next)[^'"]*|[^'"]*\.svg(?:\?[^'"]*)?)['"]/

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
 * Lists the source files of the guarded folders and root files.
 *
 * @returns Absolute paths of the `.vue` and `.ts` files.
 */
const sourceFiles = (): string[] =>
  SOURCES.flatMap((entry) => {
    const path = join(CLIENT_ROOT, entry)
    return statSync(path).isDirectory() ? filesUnder(path, ['.vue', '.ts']) : [path]
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
 * Whether a file is named in an allow list.
 *
 * @param list - The allow list.
 * @param file - Path under the client root, with forward slashes.
 * @returns True when the list names the file.
 */
const isListed = (list: readonly AllowedFile[], file: string): boolean => list.some((entry) => entry.file === file)

/**
 * Finds which rules a source file breaks.
 *
 * @param file - Path under the client root, with forward slashes.
 * @param source - File contents.
 * @returns The broken rules, each once, in rule order.
 */
const brokenRules = (file: string, source: string): IconRule[] => {
  const code = withoutComments(source)
  const broken = new Set<IconRule>()
  if (SVG_TAG.test(code) && !isListed(SVG_ALLOWED, file)) {
    broken.add(IconRule.InlineSvg)
  }
  if (SHAPE_STRING.test(code)) {
    broken.add(IconRule.ShapeMarkup)
  }
  if (GLYPH.test(code) && !isListed(GLYPH_ALLOWED, file)) {
    broken.add(IconRule.GlyphIcon)
  }
  if (ICON_IMPORT.test(code)) {
    broken.add(IconRule.IconImport)
  }
  return Object.values(IconRule).filter((rule) => broken.has(rule))
}

/** Every source file as its path under the client root and its contents. */
const FILES: ReadonlyArray<readonly [string, string]> = sourceFiles().map(
  (path) => [relative(CLIENT_ROOT, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

/** Every locale file as its path under the client root and its contents. */
const LOCALES: ReadonlyArray<readonly [string, string]> = filesUnder(join(CLIENT_ROOT, 'locales'), ['.json']).map(
  (path) => [relative(CLIENT_ROOT, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

describe('ad hoc icons', () => {
  it('reads the pages, the kit and the locales', () => {
    const files = FILES.map(([file]) => file)

    expect(files).toContain('components/ui/UiCheckbox.vue')
    expect(files).toContain('app.vue')
    expect(LOCALES.length).toBeGreaterThan(0)
  })

  it.each(FILES)('%s draws its icons through the icon library', (file, source) => {
    expect(brokenRules(file, source)).toEqual([])
  })

  it.each(LOCALES)('%s holds no emoji', (_file, source) => {
    expect(EMOJI.test(source)).toBe(false)
  })

  it.each(SVG_ALLOWED.map((entry) => [entry.file, entry.reason] as const))('%s still draws an svg (%s)', (file) => {
    const source = FILES.find(([path]) => path === file)?.[1]

    expect(source).toBeDefined()
    expect(SVG_TAG.test(withoutComments(source ?? ''))).toBe(true)
  })

  it.each(GLYPH_ALLOWED.map((entry) => [entry.file, entry.reason] as const))('%s still holds a glyph (%s)', (file) => {
    const source = FILES.find(([path]) => path === file)?.[1]

    expect(source).toBeDefined()
    expect(GLYPH.test(withoutComments(source ?? ''))).toBe(true)
  })

  describe('self test', () => {
    it('catches an inline svg outside the allowed graphics', () => {
      expect(brokenRules('components/A.vue', '<template><svg viewBox="0 0 24 24"><path d="M1 1" /></svg></template>')).toEqual([IconRule.InlineSvg])
      expect(brokenRules('components/ui/UiProgress.vue', '<template><svg /></template>')).toEqual([])
    })

    it('catches icon shapes held in a string', () => {
      expect(brokenRules('components/A.vue', "const X = '<line x1=\"1\" y1=\"1\" x2=\"2\" y2=\"2\"/>'")).toEqual([IconRule.ShapeMarkup])
    })

    it('catches a text character used as an icon', () => {
      for (const glyph of ['✓', '✕', '×', '→', '▲', '•', '⭐', '\u{1F514}']) {
        expect(brokenRules('components/A.vue', `<template><span>${glyph}</span></template>`)).toEqual([IconRule.GlyphIcon])
      }
    })

    it('lets plain punctuation, described characters and the named data files through', () => {
      expect(brokenRules('components/A.vue', '<template><span>a · b …</span></template>')).toEqual([])
      expect(brokenRules('components/A.vue', '// the ✓ is drawn by the library\nconst x = 1')).toEqual([])
      expect(brokenRules('utils/catalogGroups.ts', "const BULLET = '• '")).toEqual([])
    })

    it('catches another icon package or an svg file imported, and lets the library through', () => {
      expect(brokenRules('components/A.vue', "import { Icon } from '@iconify/vue'")).toEqual([IconRule.IconImport])
      expect(brokenRules('components/A.vue', "import logo from '~/assets/logo.svg'")).toEqual([IconRule.IconImport])
      expect(brokenRules('components/A.vue', "import { Check } from 'lucide-vue-next'")).toEqual([])
    })
  })
})
