/**
 * Tests for the i18n lint logic: missing, extra, unused and unknown-literal keys, plus dynamic
 * key prefixes counting as use and the keys declared as data in the schemas and the key tables.
 */
import { describe, expect, it } from 'vitest'
import { checkI18n, declaredKeys, flattenKeys, type Catalogues } from '../../scripts/lib/i18nCheck'

/**
 * Builds catalogues from key lists.
 *
 * @param en - English keys.
 * @param ru - Russian keys.
 * @param hy - Armenian keys.
 * @param de - German keys; the English keys when left out, so a case about the other languages stays about them.
 * @returns The catalogues.
 */
const make = (en: string[], ru: string[], hy: string[], de: string[] = en): Catalogues => ({
  en: new Set(en),
  ru: new Set(ru),
  hy: new Set(hy),
  de: new Set(de),
})

describe('flattenKeys', () => {
  it('flattens nested objects with a prefix', () => {
    expect(flattenKeys({ a: { b: 'x', c: 'y' } }, 'area')).toEqual(['area.a.b', 'area.a.c'])
  })
})

describe('checkI18n', () => {
  it('passes for complete, used catalogues', () => {
    const keys = ['common.save']
    expect(checkI18n({ catalogues: make(keys, keys, keys), source: "t('common.save')" })).toEqual([])
  })

  it('reports a key missing in a language and an extra key', () => {
    const problems = checkI18n({
      catalogues: make(['a.x'], ['a.y'], ['a.x']),
      source: "t('a.x') t('a.y')",
    })
    expect(problems).toContain('missing in ru: a.x')
    expect(problems).toContain('extra in ru (absent from en): a.y')
  })

  it('reports an unused key', () => {
    const problems = checkI18n({ catalogues: make(['a.x'], ['a.x'], ['a.x']), source: '' })
    expect(problems).toContain('unused key: a.x')
  })

  it('reports a literal t() key absent from en', () => {
    const problems = checkI18n({ catalogues: make([], [], []), source: "t('nope.key')" })
    expect(problems).toContain("t('nope.key') is used but absent from en")
  })

  it('counts a dynamic prefix as use of every key under it', () => {
    const keys = ['nav.home', 'nav.about']
    const source = 't(`nav.${name}`)'
    expect(checkI18n({ catalogues: make(keys, keys, keys), source })).toEqual([])
  })

  it('reads keys passed to translate() like t()', () => {
    const keys = ['calendar.months.1', 'common.save']
    const source = "translate('common.save') translate(`calendar.months.${i}`)"
    expect(checkI18n({ catalogues: make(keys, keys, keys), source })).toEqual([])
  })
})

describe('declaredKeys', () => {
  it('takes every dotted key literal of a screen schema and ignores paths and plain words', () => {
    const text = `import x from '@/utils/ui/skeletonSchema'
const s = { kind: 'form', title: 'clients.list.title', rows: ['shared.id'] }`

    expect([...declaredKeys([{ path: 'views/A.schema.ts', text }])].sort()).toEqual(['clients.list.title', 'shared.id'])
  })

  it('takes the keys a key table writes under its key properties, and nothing else in that file', () => {
    const text = `{ label: 'clients.nav.summary', icon: 'a.b' }
{ labelKey: 'integrations.categories.hosted' }
{ menuKey: 'reports.menu.oss', titleKey: 'shared.invoices' }`

    expect([...declaredKeys([{ path: 'lib/reports.ts', text }])].sort()).toEqual(['clients.nav.summary', 'integrations.categories.hosted', 'reports.menu.oss', 'shared.invoices'])
  })

  it('ignores a file that is neither a schema nor a key table', () => {
    expect(declaredKeys([{ path: 'lib/other.ts', text: "{ label: 'a.b' }" }]).size).toBe(0)
  })
})

describe('checkI18n declared keys', () => {
  it('reports a declared key absent from en', () => {
    const problems = checkI18n({ catalogues: make([], [], []), source: '', declared: new Set(['nope.key']) })

    expect(problems).toContain("declared key 'nope.key' is absent from en")
  })

  it('passes a declared key that exists and is quoted in the source', () => {
    const keys = ['clients.nav.summary']

    expect(checkI18n({ catalogues: make(keys, keys, keys), source: "label: 'clients.nav.summary'", declared: new Set(keys) })).toEqual([])
  })
})
