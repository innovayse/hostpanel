import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  DASHBOARD_ROWS,
  FAQ_ITEMS,
  FOOTER_COLUMNS,
  HERO_BENEFITS,
  MIGRATION_STEPS,
  NAV_ITEMS,
  PERFORMANCE_FEATURES,
  SECURITY_FEATURES,
  TRUST_ITEMS,
  USE_CASES,
  WHY_FEATURES,
} from './content'

/** The locales the portal ships; plugins/i18n.ts loads all three. */
const LOCALES = ['en', 'ru', 'hy'] as const

/**
 * Reads one locale's nova module.
 *
 * @param locale Locale directory name.
 * @returns The parsed message tree.
 */
const load = (locale: string): Record<string, unknown> =>
  JSON.parse(readFileSync(
    fileURLToPath(new URL(`../../locales/${locale}/nova.json`, import.meta.url)),
    'utf8',
  ))

/**
 * Flattens a message tree to dotted keys, which is how the components ask for
 * them.
 *
 * @param value Message tree or leaf.
 * @param prefix Dotted path accumulated so far.
 */
const flatten = (value: unknown, prefix = ''): string[] => {
  if (typeof value !== 'object' || value === null) return [prefix]

  return Object.entries(value as Record<string, unknown>)
    .flatMap(([key, child]) => flatten(child, prefix ? `${prefix}.${key}` : key))
}

const messages = Object.fromEntries(
  LOCALES.map(locale => [locale, new Set(flatten(load(locale)))]),
) as Record<(typeof LOCALES)[number], Set<string>>

/** Every i18n key `content.ts` asks the components to render. */
const referenced = [
  ...NAV_ITEMS.map(item => item.labelKey),
  ...FOOTER_COLUMNS.flatMap(column => [column.titleKey, ...column.links.map(link => link.labelKey)]),
  ...[...HERO_BENEFITS, ...TRUST_ITEMS, ...WHY_FEATURES, ...PERFORMANCE_FEATURES, ...SECURITY_FEATURES]
    .flatMap(feature => [feature.titleKey, ...(feature.bodyKey ? [feature.bodyKey] : [])]),
  ...USE_CASES.flatMap(useCase => [useCase.titleKey, useCase.bodyKey]),
  ...FAQ_ITEMS.flatMap(item => [item.questionKey, item.answerKey]),
  ...MIGRATION_STEPS.flatMap(step => [step.titleKey, step.bodyKey]),
  ...DASHBOARD_ROWS.flatMap(row => [row.labelKey, row.valueKey]),
]

describe('nova locales', () => {
  it.each(LOCALES)('translates every key content.ts references (%s)', (locale) => {
    const missing = referenced.filter(key => !messages[locale].has(key))

    expect(missing).toEqual([])
  })

  it('carries the same keys in every locale, so no language falls back mid-page', () => {
    const [first, ...rest] = LOCALES
    const reference = [...messages[first]].sort()

    for (const locale of rest) {
      expect([...messages[locale]].sort(), locale).toEqual(reference)
    }
  })
})
