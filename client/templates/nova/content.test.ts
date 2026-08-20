import { describe, expect, it } from 'vitest'
import {
  COMPARISON_LABEL_ORDER,
  DASHBOARD_ROWS,
  FAQ_ITEMS,
  FOOTER_COLUMNS,
  HERO_BENEFITS,
  MIGRATION_STEPS,
  NAV_ITEMS,
  PERFORMANCE_FEATURES,
  SECURITY_FEATURES,
  TESTIMONIALS,
  TRUST_ITEMS,
  USE_CASES,
  WHY_FEATURES,
  orderComparisonRows,
} from './content'
import type { ComparisonRow, Feature } from './types'

/**
 * Every route nova links to, taken from client/pages/. A link into this portal
 * that does not appear here is a 404 the visitor blames on the host.
 */
const ROUTES = [
  '/hosting',
  '/domains',
  '/domains/transfer',
  '/products',
  '/contact',
  '/knowledgebase',
  '/faq',
  '/announcements',
  '/client/login',
  '/terms',
  '/privacy',
  '/refund-policy',
  '/acceptable-use',
]

/** Strips any fragment, so `/hosting#plans` is checked as `/hosting`. */
const routeOf = (to: string) => to.split('#')[0]!

const allFeatures: Feature[] = [
  ...HERO_BENEFITS,
  ...TRUST_ITEMS,
  ...WHY_FEATURES,
  ...PERFORMANCE_FEATURES,
  ...SECURITY_FEATURES,
]

describe('nova navigation', () => {
  it('points every header link at a route this portal has', () => {
    for (const item of NAV_ITEMS) {
      expect(ROUTES, item.key).toContain(routeOf(item.to))
    }
  })

  it('points every footer link at a route this portal has', () => {
    for (const column of FOOTER_COLUMNS) {
      for (const link of column.links) {
        expect(ROUTES, `${column.key}/${link.key}`).toContain(routeOf(link.to))
      }
    }
  })

  it('keys every entry uniquely, so a list key cannot collide', () => {
    expect(new Set(NAV_ITEMS.map(item => item.key)).size).toBe(NAV_ITEMS.length)
    expect(new Set(FOOTER_COLUMNS.map(column => column.key)).size).toBe(FOOTER_COLUMNS.length)
  })
})

describe('nova content', () => {
  it('gives every renderable entry a key and an i18n key rather than a literal string', () => {
    const entries = [
      ...allFeatures.map(f => [f.key, f.titleKey] as const),
      ...USE_CASES.map(u => [u.key, u.titleKey] as const),
      ...FAQ_ITEMS.map(f => [f.key, f.questionKey] as const),
      ...MIGRATION_STEPS.map(s => [s.key, s.titleKey] as const),
      ...DASHBOARD_ROWS.map(r => [r.key, r.labelKey] as const),
    ]

    for (const [key, i18nKey] of entries) {
      expect(key.length, i18nKey).toBeGreaterThan(0)
      expect(i18nKey, key).toMatch(/^nova\./)
    }
  })

  it('ships no testimonials, because nothing serves any and none may be invented', () => {
    expect(TESTIMONIALS).toEqual([])
  })

  it('gates every claim about the operator\'s own hardware behind a setting', () => {
    const gated = ['storage', 'caching', 'cdn', 'firewall', 'malware', 'backups']

    for (const feature of allFeatures) {
      if (!gated.includes(feature.key)) continue
      expect(feature.requiresSetting, feature.key).toBeDefined()
    }
  })

  it('leaves the platform\'s own capabilities ungated', () => {
    const ungated = ['ssl', 'account', 'support', 'scale', 'scaling', 'servers']

    for (const feature of allFeatures) {
      if (!ungated.includes(feature.key)) continue
      expect(feature.requiresSetting, feature.key).toBeUndefined()
    }
  })
})

describe('orderComparisonRows', () => {
  const row = (label: string): ComparisonRow => ({ label, values: [] })

  it('sorts the familiar lines into the order they read best in', () => {
    const rows = [row('Databases'), row('Storage'), row('Websites')]

    expect(orderComparisonRows(rows).map(entry => entry.label))
      .toEqual(['Websites', 'Storage', 'Databases'])
  })

  it('matches a label whatever its case or surrounding words', () => {
    const rows = [row('Monthly bandwidth'), row('NVMe storage')]

    expect(orderComparisonRows(rows).map(entry => entry.label))
      .toEqual(['NVMe storage', 'Monthly bandwidth'])
  })

  it('keeps rows it does not recognise, after the ones it does', () => {
    const rows = [row('Անխափան աշխատանք'), row('Storage')]

    expect(orderComparisonRows(rows).map(entry => entry.label))
      .toEqual(['Storage', 'Անխափան աշխատանք'])
  })

  it('leaves rows of equal rank in the order the operator put them', () => {
    const rows = [row('Zone A'), row('Zone B'), row('Zone C')]

    expect(orderComparisonRows(rows).map(entry => entry.label))
      .toEqual(['Zone A', 'Zone B', 'Zone C'])
  })

  it('never adds a row and never drops one', () => {
    const rows = [row('Storage'), row('Anything'), row('CPU')]

    expect(orderComparisonRows(rows)).toHaveLength(rows.length)
    expect(orderComparisonRows([])).toEqual([])
  })

  it('lists every label the table is expected to order in lowercase, so matching works', () => {
    for (const label of COMPARISON_LABEL_ORDER) {
      expect(label).toBe(label.toLowerCase())
    }
  })
})
