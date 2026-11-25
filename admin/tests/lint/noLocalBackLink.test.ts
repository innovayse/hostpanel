/**
 * Guard: no screen draws a back link of its own.
 *
 * The way up from a page is the breadcrumb trail its `UiPageHeader` draws above the title (`crumbs`, built by
 * `usePageCrumbs`); a loose "< Domains" link between the tabs and the title is the second, worse way to say the same thing.
 * Under `src` no file may name the old back link component or the header props that drew it (`backTo`, `backLabel`,
 * `backPending`, `backMobileOnly`, `backTestid`, in either spelling), and no link or button of a screen draws the back arrow
 * (`Icon.Back`) as its icon. A file that may keep an arrow is named below with its reason, and the list is checked too: an
 * entry whose file is gone or no longer draws the arrow fails, so the list cannot rot. The scan is itself tested on planted
 * sources.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** The ways a back link is spelled: the old component and the header props of the back pill, in both casings. */
const BACK_LINK_NAMES = /\bUiBackLink\b|\b(?:backTo|backLabel|backPending|backMobileOnly|backTestid)\b|\bback-(?:to|label|pending|mobile-only|testid)\b/

/** A kit link or button opened with the back arrow as its icon. */
const BACK_ARROW_ACTION = /<(?:UiLink|UiButton)\b[^>]*?:icon="Icon\.Back"/

/** One file allowed to draw the back arrow on an action, with the reason. */
interface AllowedArrow {
  /** Path under `src`, with forward slashes. */
  file: string
  /** Why the file is not a page's way up. */
  reason: string
}

/** The named exceptions. */
const ALLOWED_ARROWS: readonly AllowedArrow[] = [
  { file: 'views/NotFoundView.vue', reason: 'a dead end outside the panel shell: the one action is the way back to the dashboard, and there is no trail to draw' },
]

/**
 * Lists every `.vue` and `.ts` file under a folder.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the sources found.
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
 * The source without its comments, so a sentence that mentions a retired name does not count as using it.
 *
 * @param source - A file's text.
 * @returns The text without block comments, HTML comments and line comments.
 */
const withoutComments = (source: string): string =>
  source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/<!--[\s\S]*?-->/g, '').replace(/^\s*\/\/.*$/gm, '')

/**
 * Finds which back link rules a source breaks.
 *
 * @param source - A file's text.
 * @returns Whether the retired names are used, and whether a link or button draws the back arrow.
 */
const breaks = (source: string): { names: boolean; arrow: boolean } => {
  const code = withoutComments(source)
  return { names: BACK_LINK_NAMES.test(code), arrow: BACK_ARROW_ACTION.test(code) }
}

/** Every guarded file as its path under `src` and its text. */
const FILES: ReadonlyArray<readonly [string, string]> = sourceFiles(SRC).map(
  (path) => [relative(SRC, path).split(sep).join('/'), readFileSync(path, 'utf8')] as const,
)

describe('no screen keeps a back link of its own', () => {
  it('names neither the retired back link component nor the header props that drew it', () => {
    const offenders = FILES.filter(([, source]) => breaks(source).names).map(([file]) => file)

    expect(offenders).toEqual([])
  })

  it('draws no back arrow on a link or a button outside the named exceptions', () => {
    const offenders = FILES.filter(([file, source]) => breaks(source).arrow && !ALLOWED_ARROWS.some((entry) => entry.file === file)).map(([file]) => file)

    expect(offenders).toEqual([])
  })

  it('keeps its exception list honest: every entry names a file that still draws the arrow', () => {
    for (const entry of ALLOWED_ARROWS) {
      const source = FILES.find(([file]) => file === entry.file)?.[1]

      expect(source, entry.file).toBeDefined()
      expect(breaks(source ?? '').arrow, entry.file).toBe(true)
      expect(entry.reason.length, entry.file).toBeGreaterThan(0)
    }
  })

  it('has no UiBackLink component left in the kit', () => {
    expect(FILES.some(([file]) => file === 'components/ui/UiBackLink.vue')).toBe(false)
  })

  describe('the scan itself', () => {
    it('catches the old component, its props in both spellings and the arrow on an action', () => {
      expect(breaks('<UiBackLink :to="x" label="Back" />').names).toBe(true)
      expect(breaks('<UiPageHeader :back-to="AppRoute.Orders" back-label="Orders" />').names).toBe(true)
      expect(breaks('defineProps<{ backTo?: string }>()').names).toBe(true)
      expect(breaks('<UiLink :icon="Icon.Back" :to="x">Back</UiLink>').arrow).toBe(true)
    })

    it('leaves alone a comment that mentions a retired name, a trail and an arrow used elsewhere', () => {
      expect(breaks('/** The UiBackLink is gone; backTo too. */\nconst a = 1').names).toBe(false)
      expect(breaks('<!-- back-to is retired -->\n<UiPageHeader :crumbs="crumbs" />').names).toBe(false)
      expect(breaks('<UiIcon :markup="Icon.Back" />').arrow).toBe(false)
      expect(breaks('const fold = computed(() => (rail.value ? Icon.Chevron : Icon.Back))').arrow).toBe(false)
    })
  })
})
