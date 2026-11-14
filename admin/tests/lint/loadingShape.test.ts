/**
 * Guard: a screen shows the real layout while it loads (#1225), never a generic placeholder in its place.
 *
 * Under `src/views`, `src/layouts` and `src/components` (the kit in `components/ui` excepted) a file must not:
 * - use a generic skeleton preset: the `UiSkeleton` component or its `UiSkeletonShape` vocabulary (a table or a card
 *   grid sketched out of nothing, a multi line block);
 * - give a `UiCard` generic placeholder lines (`skeleton-lines`) or a bare `loading` that swaps its whole body for them;
 * - hand-roll a stand-in in a `#fallback` slot: a couple of bars that fit no layout in particular.
 *
 * What a screen draws instead is its own layout in two states: the loading props of the kit (`UiPageHeader`,
 * `UiStatTile`, `UiTable`, `UiPagination`, `UiChart`, `UiProgress`, `UiField` through `UI_FIELD_LOADING`) and single
 * `UiSkeletonBar`s where a value goes. A few files may not know their shape while they load, each named below with
 * the reason. The list is checked too: an entry whose file is gone or no longer breaks its rule fails, so the list
 * cannot rot.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** The folders the guard reads. */
const ROOTS: readonly string[] = [join(SRC, 'views'), join(SRC, 'layouts'), join(SRC, 'components')]

/** The kit folder, which owns the loading states the screens use. */
const KIT = join(SRC, 'components', 'ui')

/** The rules the guard enforces. */
enum LoadingRule {
  /** The generic `UiSkeleton` component or its shape vocabulary. */
  SkeletonPreset = 'skeleton-preset',
  /** Generic placeholder lines on a card, by count or by a bare `loading` that swaps the body for them. */
  CardLines = 'card-lines',
  /** A hand-rolled stand-in in a `#fallback` slot, drawn for a body whose shape is not known. */
  SuspenseFallback = 'suspense-fallback',
}

/** One file allowed to break one rule, with the reason it may. */
interface AllowedException {
  /** Path under `src`, with forward slashes. */
  file: string
  /** The rule the file may break. */
  rule: LoadingRule
  /** Why the shape of the file's content is not known while it loads. */
  reason: string
}

/** The named exceptions: content whose layout only exists once it has arrived. */
const ALLOWED: readonly AllowedException[] = [
  {
    file: 'components/layout/ViewLoading.vue',
    rule: LoadingRule.CardLines,
    reason: 'the screen about to open is a chunk still downloading, so only a title and a card can be drawn for it',
  },
  {
    file: 'components/plugins/PluginWidget.vue',
    rule: LoadingRule.SuspenseFallback,
    reason: 'the body of a widget is drawn by the plugin itself, so only its title can be real while the body resolves',
  },
  {
    file: 'views/PluginPageView.vue',
    rule: LoadingRule.CardLines,
    reason: 'the blocks of a plugin page are defined by the plugin\'s own view document, which has not arrived yet',
  },
]

/**
 * Lists every `.vue` file under a folder, the kit left out.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the components found.
 */
const vueFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return path === KIT ? [] : vueFiles(path)
    }
    return name.endsWith('.vue') ? [path] : []
  })

/**
 * The source of a file without its comments, so a doc line may name what the file no longer uses.
 *
 * @param source - The `.vue` file contents.
 * @returns The code of the file.
 */
const codeOf = (source: string): string => source
  .replace(/<!--[\s\S]*?-->/g, '')
  .replace(/\/\*[\s\S]*?\*\//g, '')
  .replace(/(^|\s)\/\/.*$/gm, '$1')

/** The generic skeleton component, imported or used (a `UiSkeletonBar` is a single bar and does not match). */
const SKELETON_PRESET = /\bUiSkeleton(?![A-Za-z])|UiSkeletonShape|ui\/skeleton\b/

/** Generic lines on a card: the count prop in either spelling. */
const CARD_LINES_PROP = /\bskeleton-lines\b|\bskeletonLines\b/

/**
 * A `UiCard` opening tag carrying the `loading` flag, bare or bound. A quoted attribute value is skipped whole, so a
 * `>` inside one (`:class="rows > 2 ? ..."`) does not end the tag and a `loading` inside one is not the flag.
 */
const CARD_LOADING = /<UiCard\b(?:[^>"']|"[^"]*"|'[^']*')*?\s:?loading(?=[\s=/>])/

/** A named `#fallback` slot, where a stand-in for a body of unknown shape is written by hand. */
const SUSPENSE_FALLBACK = /<template\s+(?:#|v-slot:)fallback\b/

/**
 * Finds which rules a file breaks.
 *
 * @param code - The file's code without comments.
 * @returns The broken rules, each once, in rule order.
 */
const brokenRules = (code: string): LoadingRule[] => {
  const broken = new Set<LoadingRule>()
  if (SKELETON_PRESET.test(code)) {
    broken.add(LoadingRule.SkeletonPreset)
  }
  if (CARD_LINES_PROP.test(code) || CARD_LOADING.test(code)) {
    broken.add(LoadingRule.CardLines)
  }
  if (SUSPENSE_FALLBACK.test(code)) {
    broken.add(LoadingRule.SuspenseFallback)
  }
  return Object.values(LoadingRule).filter((rule) => broken.has(rule))
}

/**
 * Whether a file may break a rule.
 *
 * @param file - Path under `src`, with forward slashes.
 * @param rule - The broken rule.
 * @returns True when the allow list names the pair.
 */
const isAllowed = (file: string, rule: LoadingRule): boolean =>
  ALLOWED.some((entry) => entry.file === file && entry.rule === rule)

/** Every guarded file as its path under `src` and its code. */
const FILES: ReadonlyArray<readonly [string, string]> = ROOTS.flatMap(vueFiles).map(
  (path) => [relative(SRC, path).split(sep).join('/'), codeOf(readFileSync(path, 'utf8'))] as const,
)

describe('loading shape', () => {
  it('reads the screens, the layouts and the domain components, and leaves the kit out', () => {
    const files = FILES.map(([file]) => file)

    expect(files).toContain('views/ServersView.vue')
    expect(files).toContain('layouts/ClientLayout.vue')
    expect(files).toContain('components/plugins/PluginRegistryCard.vue')
    expect(files.some((file) => file.startsWith('components/ui/'))).toBe(false)
  })

  it.each(FILES)('%s draws no generic placeholder in place of its layout', (file, code) => {
    expect(brokenRules(code).filter((rule) => !isAllowed(file, rule))).toEqual([])
  })

  it.each(ALLOWED.map((entry) => [entry.file, entry.rule, entry.reason] as const))(
    '%s still needs its %s exception (%s)',
    (file, rule) => {
      const code = FILES.find(([path]) => path === file)?.[1]

      expect(code).toBeDefined()
      expect(brokenRules(code ?? '')).toContain(rule)
    },
  )

  describe('self test', () => {
    it('catches the skeleton component and its shapes', () => {
      expect(brokenRules("import UiSkeleton from '@/components/ui/UiSkeleton.vue'")).toEqual([LoadingRule.SkeletonPreset])
      expect(brokenRules('<UiSkeleton :lines="3" />')).toEqual([LoadingRule.SkeletonPreset])
      expect(brokenRules("import { UiSkeletonShape } from '@/utils/ui/skeleton'")).toEqual([LoadingRule.SkeletonPreset])
    })

    it('catches generic card lines, by count or by a bare loading card', () => {
      expect(brokenRules('<UiCard loading :skeleton-lines="4" />')).toEqual([LoadingRule.CardLines])
      expect(brokenRules('<UiCard padding="md" loading />')).toEqual([LoadingRule.CardLines])
      expect(brokenRules('<UiCard padding="md" :loading="store.loading">')).toEqual([LoadingRule.CardLines])
    })

    it('reads past a `>` inside a quoted attribute value of the card tag', () => {
      expect(brokenRules('<UiCard :class="rows > 2 ? \'a\' : \'b\'" loading>')).toEqual([LoadingRule.CardLines])
      expect(brokenRules('<UiCard :class="rows > 2 ? \'a\' : \'b\'" :aria-busy="loading ? \'true\' : undefined">')).toEqual([])
    })

    it('catches a hand-rolled stand-in in a fallback slot', () => {
      expect(brokenRules('<Suspense><slot /><template #fallback><UiSkeletonBar /><UiSkeletonBar /></template></Suspense>')).toEqual([LoadingRule.SuspenseFallback])
      expect(brokenRules('<template v-slot:fallback><UiSkeletonBar /></template>')).toEqual([LoadingRule.SuspenseFallback])
      expect(brokenRules('<template #actions><UiButton /></template>')).toEqual([])
    })

    it('passes single bars, the kit loading props and a card without the flag', () => {
      expect(brokenRules('<UiSkeletonBar class="h-4 w-20 rounded-md" />')).toEqual([])
      expect(brokenRules('<UiTable :loading="initialLoading" /><UiPageHeader loading /><UiStatTile loading />')).toEqual([])
      expect(brokenRules('<UiCard padding="md" :aria-busy="loading ? \'true\' : undefined">')).toEqual([])
    })

    it('ignores a comment that names what the file no longer uses', () => {
      expect(codeOf('<!-- <UiSkeleton /> -->/** UiSkeletonShape */<UiStack />')).toBe('<UiStack />')
    })
  })
})
