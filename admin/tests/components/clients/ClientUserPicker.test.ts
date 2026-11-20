/**
 * Mount tests for ClientUserPicker: debounced search, choosing a hit and clearing the choice.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import ClientUserPicker from '@/components/clients/ClientUserPicker.vue'
import type { UserSearchHit } from '@/types/clientuser'
import { createTestI18n } from '../../helpers/i18n'

/** A search hit used across the tests. */
const ADA: UserSearchHit = { id: 'u1', email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace' }

/**
 * Mounts the picker with a scripted search.
 *
 * @param search - The lookup the picker calls.
 * @param modelValue - The chosen user, if any.
 * @returns The wrapper.
 */
const mountPicker = (search: (term: string) => Promise<UserSearchHit[]>, modelValue: UserSearchHit | null = null) =>
  mount(ClientUserPicker, {
    props: { modelValue, search, testid: 'picker' },
    global: { plugins: [createTestI18n()] },
  })

describe('ClientUserPicker', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('searches after the debounce and lists the hits', async () => {
    const search = vi.fn().mockResolvedValue([ADA])
    const wrapper = mountPicker(search)

    await wrapper.get('[data-testid="picker-search"]').setValue('ada')
    expect(search).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()

    expect(search).toHaveBeenCalledWith('ada')
    expect(wrapper.find('[data-testid="picker-result-u1"]').exists()).toBe(true)
  })

  it('emits the chosen hit', async () => {
    const wrapper = mountPicker(vi.fn().mockResolvedValue([ADA]))

    await wrapper.get('[data-testid="picker-search"]').setValue('ada')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    await wrapper.get('[data-testid="picker-result-u1"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[ADA]])
  })

  it('shows the chosen user and clears it', async () => {
    const wrapper = mountPicker(vi.fn(), ADA)

    expect(wrapper.get('[data-testid="picker-selected"]').text()).toContain('ada@example.com')
    await wrapper.get('[data-testid="picker-clear"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
  })

  it('draws rows of skeleton bars where the hits will appear while the search is in flight', async () => {
    const wrapper = mountPicker(vi.fn(() => new Promise<UserSearchHit[]>(() => undefined)))

    await wrapper.get('[data-testid="picker-search"]').setValue('ada')
    await vi.advanceTimersByTimeAsync(300)

    const loading = wrapper.get('[data-testid="picker-loading"]')
    expect(loading.attributes('aria-busy')).toBe('true')
    expect(loading.findAll('[data-testid="ui-skeleton-bar"]').length).toBeGreaterThan(0)
    expect(wrapper.find('[data-testid="picker-result-u1"]').exists()).toBe(false)
  })

  it('replaces the skeleton rows with the hits once the search answers', async () => {
    const wrapper = mountPicker(vi.fn().mockResolvedValue([ADA]))

    await wrapper.get('[data-testid="picker-search"]').setValue('ada')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()

    expect(wrapper.find('[data-testid="picker-loading"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="picker-result-u1"]').exists()).toBe(true)
  })
})
