/**
 * Tests for the result banner: a success goes to the toast store and never shows inline, a refusal shows inline and
 * hides after the delay, a newer refusal restarts the delay, and the pending timer is cancelled on unmount.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useResultBanner } from '@/composables/useResultBanner'
import type { ResultBanner } from '@/composables/useResultBanner'
import { TOAST_MS, useToastStore } from '@/stores/toast'

/**
 * Mounts a throwaway component that owns a banner, so the unmount hook is real.
 *
 * @returns The banner and the wrapper.
 */
const mountBanner = () => {
  let banner: ResultBanner | undefined
  const wrapper = mount(defineComponent({
    setup: () => {
      banner = useResultBanner()
      return () => h('div')
    },
  }))
  return { banner: banner as ResultBanner, wrapper }
}

describe('useResultBanner', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('toasts a success and keeps the banner empty', () => {
    const { banner } = mountBanner()

    banner.show('Rates updated', false)

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Rates updated'])
    expect(banner.message.value).toBeNull()
    expect(banner.isError.value).toBe(false)

    vi.advanceTimersByTime(TOAST_MS)
    expect(useToastStore().toasts).toEqual([])
  })

  it('shows a refusal inline with its error flag and hides it after the delay', () => {
    const { banner } = mountBanner()

    banner.show('Could not reach the server', true)

    expect(banner.message.value).toBe('Could not reach the server')
    expect(banner.isError.value).toBe(true)
    expect(useToastStore().toasts).toEqual([])

    vi.advanceTimersByTime(TOAST_MS)
    expect(banner.message.value).toBeNull()
    expect(banner.isError.value).toBe(false)
  })

  it('a newer refusal replaces the older one and restarts the delay', () => {
    const { banner } = mountBanner()

    banner.show('first', true)
    vi.advanceTimersByTime(TOAST_MS - 1000)
    banner.show('second', true)
    vi.advanceTimersByTime(TOAST_MS - 1000)

    expect(banner.message.value).toBe('second')
    vi.advanceTimersByTime(1000)
    expect(banner.message.value).toBeNull()
  })

  it('a success clears an earlier refusal', () => {
    const { banner } = mountBanner()

    banner.show('failed', true)
    banner.show('saved', false)

    expect(banner.message.value).toBeNull()
    expect(useToastStore().toasts).toHaveLength(1)
  })

  it('clear hides the banner at once', () => {
    const { banner } = mountBanner()

    banner.show('x', true)
    banner.clear()

    expect(banner.message.value).toBeNull()
    expect(banner.isError.value).toBe(false)
  })

  it('drops its pending timer when the screen unmounts', () => {
    const { banner, wrapper } = mountBanner()
    banner.show('pending', true)
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })
})
