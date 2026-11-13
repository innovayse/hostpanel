/**
 * Tests for the command palette's pure logic: the entries built from the sidebar definition, the search,
 * the label split for highlighting and the per-viewer recent list kept in storage.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  buildPaletteEntries,
  filterPaletteEntries,
  MAX_RECENT,
  PaletteGroup,
  readRecent,
  rememberRecent,
  splitLabel,
} from '@/lib/commandPalette'
import { NAV_SECTIONS } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'

/**
 * Stand-in translation: returns the key itself, so a test reads the key a label came from.
 *
 * @param key - The i18n key.
 * @returns The same key.
 */
const echo = (key: string): string => key

describe('buildPaletteEntries', () => {
  it('lists the create actions first, then the sidebar pages', () => {
    const entries = buildPaletteEntries(NAV_SECTIONS, echo)

    expect(entries.at(0)?.group).toBe(PaletteGroup.Actions)
    expect(entries.at(0)?.to).toBe(AppRoute.ClientsAdd)
    expect(entries.some((entry) => entry.group === PaletteGroup.Navigation && entry.to === AppRoute.Dashboard)).toBe(true)
  })

  it('lists a sub-page under its parent and never lists a path twice', () => {
    const entries = buildPaletteEntries(NAV_SECTIONS, echo)
    const users = entries.find((entry) => entry.to === AppRoute.ClientsUsers)
    const paths = entries.map((entry) => entry.to)

    expect(users?.hint).toBe('nav.clients')
    expect(new Set(paths).size).toBe(paths.length)
  })

  it('takes the label of a plugin entry as the API sent it', () => {
    const entries = buildPaletteEntries(
      NAV_SECTIONS.slice(0, 1).map((section) => ({ id: section.id, labelKey: 'nav.sections.addons', items: [{ to: '/apps/x', label: 'Plugin page', icon: '<path d="M0 0"/>' }] })),
      echo,
    )

    expect(entries.find((entry) => entry.to === '/apps/x')?.label).toBe('Plugin page')
  })
})

describe('filterPaletteEntries', () => {
  const entries = buildPaletteEntries(NAV_SECTIONS, echo)

  it('returns everything for a blank search', () => {
    expect(filterPaletteEntries(entries, '   ')).toHaveLength(entries.length)
  })

  it('matches case-insensitively on the label and the hint', () => {
    const found = filterPaletteEntries(entries, 'NAV.CLIENTSCHILDREN.USERS')

    expect(found.map((entry) => entry.to)).toEqual([AppRoute.ClientsUsers])
  })

  it('returns nothing when no entry matches', () => {
    expect(filterPaletteEntries(entries, 'zzzzzz')).toEqual([])
  })
})

describe('splitLabel', () => {
  it('splits around the first match', () => {
    expect(splitLabel('Create client', 'ate c')).toEqual([
      { text: 'Cre', match: false, start: 0 },
      { text: 'ate c', match: true, start: 3 },
      { text: 'lient', match: false, start: 8 },
    ])
  })

  it('returns the label whole for a blank or absent search', () => {
    expect(splitLabel('Orders', '')).toEqual([{ text: 'Orders', match: false, start: 0 }])
    expect(splitLabel('Orders', 'xyz')).toEqual([{ text: 'Orders', match: false, start: 0 }])
  })

  it('has no empty piece when the match is the whole label', () => {
    expect(splitLabel('Orders', 'orders')).toEqual([{ text: 'Orders', match: true, start: 0 }])
  })
})

describe('recent entries', () => {
  afterEach(() => {
    localStorage.clear()
    vi.restoreAllMocks()
  })

  it('moves a path to the front without repeating it and caps the list', () => {
    let list: string[] = []
    for (let i = 0; i < MAX_RECENT + 2; i += 1) {
      list = rememberRecent('a@x.test', list, `/p${i}`)
    }
    list = rememberRecent('a@x.test', list, '/p3')

    expect(list[0]).toBe('/p3')
    expect(list).toHaveLength(MAX_RECENT)
    expect(new Set(list).size).toBe(list.length)
  })

  it('keeps each viewer apart', () => {
    rememberRecent('a@x.test', [], '/a')
    rememberRecent('b@x.test', [], '/b')

    expect(readRecent('a@x.test')).toEqual(['/a'])
    expect(readRecent('b@x.test')).toEqual(['/b'])
  })

  it('stores nothing for nobody', () => {
    expect(rememberRecent('', [], '/a')).toEqual(['/a'])
    expect(readRecent('')).toEqual([])
    expect(localStorage.length).toBe(0)
  })

  it('reads corrupt or non-list storage as empty', () => {
    localStorage.setItem('hostpanel.admin.commandPalette.recent.a@x.test', '{oops')
    expect(readRecent('a@x.test')).toEqual([])
    localStorage.setItem('hostpanel.admin.commandPalette.recent.a@x.test', '{"a":1}')
    expect(readRecent('a@x.test')).toEqual([])
  })

  it('survives a storage that throws', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('full')
    })

    expect(readRecent('a@x.test')).toEqual([])
    expect(rememberRecent('a@x.test', [], '/a')).toEqual(['/a'])
  })
})
