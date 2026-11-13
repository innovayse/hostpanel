/**
 * Tests for the sidebar store: it opens by default, folds and unfolds, and remembers the choice in
 * localStorage so a new store (a reload) starts the way the operator left it.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { SIDEBAR_STORAGE_KEY, useSidebarStore } from '@/stores/sidebar'

describe('sidebar store', () => {
  beforeEach(() => {
    window.localStorage.clear()
    setActivePinia(createPinia())
  })

  it('starts open when nothing is stored', () => {
    expect(useSidebarStore().collapsed).toBe(false)
  })

  it('toggles and stores the choice', () => {
    const store = useSidebarStore()

    store.toggle()
    expect(store.collapsed).toBe(true)
    expect(window.localStorage.getItem(SIDEBAR_STORAGE_KEY)).toBe('1')

    store.toggle()
    expect(store.collapsed).toBe(false)
    expect(window.localStorage.getItem(SIDEBAR_STORAGE_KEY)).toBe('0')
  })

  it('starts folded after a reload that follows a fold', () => {
    useSidebarStore().setCollapsed(true)
    setActivePinia(createPinia())

    expect(useSidebarStore().collapsed).toBe(true)
  })
})
