/**
 * Pure logic of the i18n lint: compares the `en`, `ru`, `hy`, `de` and `es` catalogues and the keys the
 * source uses. File access is injected so the same code runs from the CLI and from tests.
 */

/** The languages every catalogue ships. */
export const LANGUAGES = ['en', 'ru', 'hy', 'de', 'es'] as const

/** One of the shipped languages. */
export type Language = (typeof LANGUAGES)[number]

/** Flat key sets per language. */
export type Catalogues = Record<Language, Set<string>>

/** Everything the check needs, already read from disk. */
export interface I18nInput {
  /** Flat message keys per language (`area.some.key`). */
  catalogues: Catalogues
  /** Concatenated source text of every `.vue` and `.ts` file under `src`. */
  source: string
  /** Locale keys the source declares as data rather than passes to `t()`: see {@link declaredKeys}. Optional. */
  declared?: ReadonlySet<string>
}

/** One source file: its path under `src` with forward slashes, and its text. */
export interface SourceFile {
  /** Path under `src`, such as `views/AddClientView.schema.ts`. */
  path: string
  /** The file's text. */
  text: string
}

/** The source files, besides the screen schemas, whose tables hold locale keys (the section tabs, the report menu, the integration categories). */
export const KEY_TABLE_FILES: readonly string[] = ['lib/clientSections.ts', 'lib/reports.ts', 'types/integrationmeta.ts']

/** A string literal that is a dotted locale key: letters and digits in two or more segments, no path or address characters, and not the value of a `key` property (a column's data path such as `dates.created`). */
const KEY_LITERAL = /(?<!\bkey\s*:\s*)(['"])([a-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+)+)\1/g

/** A locale key written as the value of a key property (`label`, `labelKey`, `menuKey`, `titleKey`) in a key table. */
const KEY_PROPERTY = /\b(?:label|labelKey|menuKey|titleKey)\s*:\s*(['"])([a-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+)+)\1/g

/**
 * Collects the locale keys the source declares as data: every dotted key literal of a screen schema (`*.schema.ts`, whose
 * texts are all keys) and the keys a key table writes under `label`, `labelKey`, `menuKey` or `titleKey`.
 *
 * @param files - The source files under `src`.
 * @returns The declared keys, each of which must exist in `en`.
 */
export const declaredKeys = (files: readonly SourceFile[]): Set<string> => {
  const found = new Set<string>()
  for (const file of files) {
    if (file.path.endsWith('.schema.ts')) {
      for (const match of file.text.matchAll(KEY_LITERAL)) {
        found.add(match[2] as string)
      }
    } else if (KEY_TABLE_FILES.includes(file.path)) {
      for (const match of file.text.matchAll(KEY_PROPERTY)) {
        found.add(match[2] as string)
      }
    }
  }
  return found
}

/**
 * Flattens a nested message object into dotted keys.
 *
 * @param value - Parsed JSON.
 * @param prefix - Key prefix for this level (an area name or a parent path).
 * @returns The dotted leaf keys.
 */
export const flattenKeys = (value: unknown, prefix = ''): string[] => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return prefix ? [prefix] : []
  }
  return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) =>
    flattenKeys(child, prefix ? `${prefix}.${key}` : key),
  )
}

/**
 * Extracts the literal keys passed to `t('key')` / `$t('key')` / `i18n.global.t('key')` / `translate('key')` / `translateIn('key', locale)` / `translateOrRaw('key', raw)`.
 *
 * @param source - Source text.
 * @returns The set of literal keys.
 */
export const literalKeys = (source: string): Set<string> => {
  const found = new Set<string>()
  for (const match of source.matchAll(/(?<![\w$])(?:\$?t|translate|translateIn|translateOrRaw)\(\s*(['"])([^'"`]+)\1/g)) {
    found.add(match[2] as string)
  }
  return found
}

/**
 * Extracts prefixes of dynamic keys such as t(`nav.${name}`): every key under them counts as used.
 *
 * @param source - Source text.
 * @returns The dynamic prefixes, each ending right before the interpolation.
 */
export const dynamicPrefixes = (source: string): string[] => {
  const found: string[] = []
  for (const match of source.matchAll(/(?<![\w$])(?:\$?t|translate|translateIn|translateOrRaw)\(\s*`([^`$]*)\$\{/g)) {
    found.push(match[1] as string)
  }
  return found
}

/**
 * Runs every check.
 *
 * @param input - The catalogues and the source text.
 * @returns One message per problem; empty when the catalogues are clean.
 */
export const checkI18n = (input: I18nInput): string[] => {
  const problems: string[] = []
  const en = input.catalogues.en

  for (const language of LANGUAGES) {
    if (language === 'en') {
      continue
    }
    for (const key of en) {
      if (!input.catalogues[language].has(key)) {
        problems.push(`missing in ${language}: ${key}`)
      }
    }
    for (const key of input.catalogues[language]) {
      if (!en.has(key)) {
        problems.push(`extra in ${language} (absent from en): ${key}`)
      }
    }
  }

  for (const key of literalKeys(input.source)) {
    if (!en.has(key)) {
      problems.push(`t('${key}') is used but absent from en`)
    }
  }

  for (const key of input.declared ?? []) {
    if (!en.has(key)) {
      problems.push(`declared key '${key}' is absent from en`)
    }
  }

  const prefixes = dynamicPrefixes(input.source)
  for (const key of en) {
    const quoted = [`'${key}'`, `"${key}"`, `\`${key}\``].some((q) => input.source.includes(q))
    if (!quoted && !prefixes.some((p) => key.startsWith(p))) {
      problems.push(`unused key: ${key}`)
    }
  }

  return problems
}
