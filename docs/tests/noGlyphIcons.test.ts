/**
 * Guard: the documentation theme draws no icon from a text character. A check, a cross, an arrow, a bullet, a triangle, a
 * star or an emoji used as an icon, and an inline `<svg>`, are replaced by a component of the icon library
 * (`lucide-vue-next`). The test reads every `.vue` and `.ts` file of `.vitepress`, comments removed, and fails with the
 * file when it holds one.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `.vitepress` folder. */
const ROOT = join(__dirname, '..', '.vitepress')

/** Text characters that read as icons: dingbats, arrows, bullets, geometric shapes, the information sign, the multiplication sign, emoji. */
const GLYPH = /[←-⇿•‣⋮⋯■-◿☀-➿⬀-⯿×ℹ️\u{1F000}-\u{1FAFF}]/u

/** An opening `<svg` tag. */
const SVG_TAG = /<svg[\s>]/

/**
 * Lists the `.vue` and `.ts` files of a folder, walking down; the VitePress cache and build output are left out.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths found.
 */
const sourceFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return name === 'cache' || name === 'dist' ? [] : sourceFiles(path)
    }
    return name.endsWith('.vue') || name.endsWith('.ts') ? [path] : []
  })

/**
 * Removes comments, so a character that is only described is not counted.
 *
 * @param source - File contents.
 * @returns The contents without HTML, block and line comments.
 */
const withoutComments = (source: string): string =>
  source
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:'"`])\/\/[^\n]*/g, '$1')

/** Every source file as its path under `.vitepress` and its contents. */
const FILES: ReadonlyArray<readonly [string, string]> = sourceFiles(ROOT).map(
  (path) => [relative(ROOT, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

describe('glyph icons', () => {
  it('reads the theme components', () => {
    expect(FILES.map(([file]) => file)).toContain('theme/components/Callout.vue')
  })

  it.each(FILES)('%s draws no icon from a text character', (_file, source) => {
    expect(GLYPH.test(withoutComments(source))).toBe(false)
  })

  it.each(FILES)('%s draws no inline svg', (_file, source) => {
    expect(SVG_TAG.test(withoutComments(source))).toBe(false)
  })

  describe('self test', () => {
    it('catches a check, a cross, an arrow, an information sign and an emoji', () => {
      for (const glyph of ['✓', '✕', '→', 'ℹ', '⚠', '\u{1F514}']) {
        expect(GLYPH.test(glyph)).toBe(true)
      }
    })

    it('catches an inline svg', () => {
      expect(SVG_TAG.test('<template><svg viewBox="0 0 24 24"><path d="M1 1" /></svg></template>')).toBe(true)
      expect(SVG_TAG.test('<template><Search class="icon" /></template>')).toBe(false)
    })

    it('lets plain punctuation and described characters through', () => {
      expect(GLYPH.test(withoutComments('a · b … // the ✓ is drawn by the library'))).toBe(false)
    })
  })
})
