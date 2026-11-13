/**
 * Tests for the command palette store: opening, closing and toggling, and the recent pages kept per viewer.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { useCommandPaletteStore } from '@/stores/commandPalette'

describe('command palette store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    localStorage.clear()
  })

  it('starts closed and opens, closes and toggles', () => {
    const store = useCommandPaletteStore()

    expect(store.isOpen).toBe(false)
    store.open()
    expect(store.isOpen).toBe(true)
    store.close()
    expect(store.isOpen).toBe(false)
    store.toggle()
    expect(store.isOpen).toBe(true)
    store.toggle()
    expect(store.isOpen).toBe(false)
  })

  it('remembers pages for the signed-in viewer and reads them back on open', () => {
    useAuthStore().user = { email: 'a@x.test', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'] }
    const store = useCommandPaletteStore()

    store.remember('/clients')
    store.remember('/orders')
    store.recent = []
    store.open()

    expect(store.recent).toEqual(['/orders', '/clients'])
  })

  it('does not show one viewer the history of another', () => {
    const auth = useAuthStore()
    auth.user = { email: 'a@x.test', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'] }
    const store = useCommandPaletteStore()
    store.remember('/clients')

    auth.user = { email: 'b@x.test', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'] }
    store.open()

    expect(store.recent).toEqual([])
  })
})
