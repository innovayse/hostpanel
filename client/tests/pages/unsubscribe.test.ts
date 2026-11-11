/**
 * Tests for `pages/unsubscribe.vue` against the real unsubscribe store: the panels the attempt
 * walks through. Only the API composable is replaced.
 *
 * @module tests/pages/unsubscribe.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAuthPage, stubAuthRuntime } from '~/tests/helpers/authScreens'

/** The mocked unsubscribe call, re-created for every test. */
const unsubscribe = vi.fn()

/** The `useSeoMeta` spy, to read the robots tag the page sets. */
const useSeoMeta = vi.fn()

vi.mock('~/composables/apis/useUnsubscribeApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns Only the endpoint this page reaches through the store.
   */
  useUnsubscribeApi: () => ({ unsubscribe })
}))

const { default: UnsubscribePage } = await import('~/pages/unsubscribe.vue')

beforeEach(() => {
  unsubscribe.mockReset()
  useSeoMeta.mockReset()
  vi.stubGlobal('useSeoMeta', useSeoMeta)
})

describe('unsubscribe page', () => {
  it('sends the token of the link and confirms the unsubscribe', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    unsubscribe.mockResolvedValue('')

    const wrapper = await mountAuthPage(UnsubscribePage)
    await flushPromises()

    expect(unsubscribe).toHaveBeenCalledWith('tok')
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="unsubscribe-failed"]').exists()).toBe(false)
  })

  it('shows the API sentence when the link is refused', async () => {
    stubAuthRuntime({ query: { token: 'bad' } })
    unsubscribe.mockRejectedValue({ data: { detail: 'This link is not valid.' } })

    const wrapper = await mountAuthPage(UnsubscribePage)
    await flushPromises()

    expect(wrapper.find('[data-testid="unsubscribe-failed"]').text()).toContain('This link is not valid.')
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(false)
  })

  it('asks the API about a link without a token, so its sentence is the one shown', async () => {
    stubAuthRuntime({ query: {} })
    unsubscribe.mockRejectedValue({ data: { detail: 'A token is required.' } })

    const wrapper = await mountAuthPage(UnsubscribePage)
    await flushPromises()

    expect(unsubscribe).toHaveBeenCalledWith('')
    expect(wrapper.find('[data-testid="unsubscribe-failed"]').text()).toContain('A token is required.')
  })

  it('shows the working line while the request is open', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    unsubscribe.mockReturnValue(new Promise(() => undefined))

    const wrapper = await mountAuthPage(UnsubscribePage)

    expect(wrapper.find('[data-testid="unsubscribe-pending"]').exists()).toBe(true)
  })

  it('keeps itself out of search results', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    unsubscribe.mockResolvedValue('')

    await mountAuthPage(UnsubscribePage)

    expect(useSeoMeta).toHaveBeenCalledWith({ robots: 'noindex, nofollow' })
  })
})
