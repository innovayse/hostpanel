/**
 * Tests for the page title store: the first header on display names the page, a header changes or withdraws only
 * its own heading, and an empty title counts as no title.
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
})
