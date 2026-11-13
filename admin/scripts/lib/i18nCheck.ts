/**
 * Pure logic of the i18n lint: compares the `en`, `ru` and `hy` catalogues and the keys the
 * source uses. File access is injected so the same code runs from the CLI and from tests.
 */

/** The languages every catalogue ships. */
export const LANGUAGES = ['en', 'ru', 'hy', 'es'] as const

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
 * Extracts the literal keys passed to `t('key')` / `$t('key')` / `i18n.global.t('key')` / `translate('key')` / `translateOrRaw('key', raw)`.
 *
 * @param source - Source text.
 * @returns The set of literal keys.
 */
export const literalKeys = (source: string): Set<string> => {
  const found = new Set<string>()
  for (const match of source.matchAll(/(?<![\w$])(?:\$?t|translate|translateOrRaw)\(\s*(['"])([^'"`]+)\1/g)) {
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
  for (const match of source.matchAll(/(?<![\w$])(?:\$?t|translate|translateOrRaw)\(\s*`([^`$]*)\$\{/g)) {
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

  const prefixes = dynamicPrefixes(input.source)
  for (const key of en) {
    const quoted = [`'${key}'`, `"${key}"`, `\`${key}\``].some((q) => input.source.includes(q))
    if (!quoted && !prefixes.some((p) => key.startsWith(p))) {
      problems.push(`unused key: ${key}`)
    }
  }

  return problems
}
