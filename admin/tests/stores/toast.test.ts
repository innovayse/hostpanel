/**
 * Unit tests for the toast store: a toast is added with its tone, dismisses itself after its time, can be dismissed
 * early, and `clear` drops every toast and every pending timer.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { TOAST_MS, useToastStore } from '@/stores/toast'
import { UiTone } from '@/utils/ui/tone'

describe('toast store', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('adds a success toast by default, each with its own id', () => {
    const store = useToastStore()

    const first = store.push('Saved')
    const second = store.push('Deleted', UiTone.Info)

    expect(first).not.toBe(second)
    expect(store.toasts).toEqual([
      { id: first, message: 'Saved', tone: UiTone.Success },
      { id: second, message: 'Deleted', tone: UiTone.Info },
    ])
  })

  it('dismisses a toast by itself after its time and no sooner', () => {
    const store = useToastStore()
    store.push('Saved')

    vi.advanceTimersByTime(TOAST_MS - 1)
    expect(store.toasts).toHaveLength(1)

    vi.advanceTimersByTime(1)
    expect(store.toasts).toEqual([])
    expect(vi.getTimerCount()).toBe(0)
  })

  it('dismisses one toast early and leaves the others', () => {
    const store = useToastStore()
    const first = store.push('one')
    store.push('two')

    store.dismiss(first)

    expect(store.toasts.map((toast) => toast.message)).toEqual(['two'])
    expect(vi.getTimerCount()).toBe(1)
  })

  it('ignores an id that is not on screen', () => {
    const store = useToastStore()
    store.push('one')

    store.dismiss(999)

    expect(store.toasts).toHaveLength(1)
  })

  it('clears every toast and every timer', () => {
    const store = useToastStore()
    store.push('one')
    store.push('two')

    store.clear()

    expect(store.toasts).toEqual([])
    expect(vi.getTimerCount()).toBe(0)
  })
})
