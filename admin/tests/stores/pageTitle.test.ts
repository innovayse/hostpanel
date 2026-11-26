/**
 * Tests for the page title store: the first header on display names the page, a header changes or withdraws only
 * its own heading, an empty title counts as no title, and a header that draws the page's trail owns it.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { usePageTitleStore } from '@/stores/pageTitle'

describe('page title store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('has no title until a header announces one', () => {
    expect(usePageTitleStore().title).toBeUndefined()
  })

  it('takes the title of the first header on display and keeps it while a second one comes and goes', () => {
    const store = usePageTitleStore()
    const page = Symbol('page')
    const nested = Symbol('nested')

    store.announce(page, 'Ada Lovelace')
    store.announce(nested, 'Notes')
    expect(store.title).toBe('Ada Lovelace')

    store.release(nested)
    expect(store.title).toBe('Ada Lovelace')
  })

  it('changes the title of the header that announced it, in place', () => {
    const store = usePageTitleStore()
    const page = Symbol('page')

    store.announce(page, 'Clients')
    store.announce(Symbol('other'), 'Other')
    store.announce(page, 'Ada Lovelace')

    expect(store.title).toBe('Ada Lovelace')
  })

  it('withdraws a title that becomes empty or undefined and one that is released', () => {
    const store = usePageTitleStore()
    const page = Symbol('page')

    store.announce(page, 'Clients')
    store.announce(page, undefined)
    expect(store.title).toBeUndefined()

    store.announce(page, 'Clients')
    store.announce(page, '')
    expect(store.title).toBeUndefined()

    store.announce(page, 'Clients')
    store.release(page)
    expect(store.title).toBeUndefined()
  })

  it('owns no trail until a header says it draws one', () => {
    const store = usePageTitleStore()

    store.announce(Symbol('page'), 'Clients')

    expect(store.ownsTrail).toBe(false)
  })

  it('owns the trail while a header that draws it is on display, even before its title has loaded', () => {
    const store = usePageTitleStore()
    const page = Symbol('page')

    store.announce(page, undefined, true)
    expect(store.ownsTrail).toBe(true)
    expect(store.title).toBeUndefined()

    store.announce(page, 'Ada Lovelace', true)
    expect(store.ownsTrail).toBe(true)
    expect(store.title).toBe('Ada Lovelace')
  })

  it('gives the trail back when the header stops drawing it or goes away', () => {
    const store = usePageTitleStore()
    const page = Symbol('page')

    store.announce(page, 'Ada Lovelace', true)
    store.announce(page, 'Ada Lovelace', false)
    expect(store.ownsTrail).toBe(false)

    store.announce(page, 'Ada Lovelace', true)
    store.release(page)
    expect(store.ownsTrail).toBe(false)
  })

  it('tells a layout whether a header other than itself draws the trail', () => {
    const store = usePageTitleStore()
    const layout = Symbol('layout')
    const header = Symbol('header')

    store.announce(layout, undefined, true)
    expect(store.trailOwnedElsewhere(layout)).toBe(false)

    store.announce(header, 'Domain', true)
    expect(store.trailOwnedElsewhere(layout)).toBe(true)
    expect(store.trailOwnedElsewhere(header)).toBe(true)

    store.release(layout)
    expect(store.trailOwnedElsewhere(header)).toBe(false)
  })
})
