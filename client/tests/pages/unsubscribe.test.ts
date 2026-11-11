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

/**
 * Mounts the page and presses the confirm button, as the reader does.
 *
 * @returns The wrapper after the request has settled.
 */
const mountAndConfirm = async () => {
  const wrapper = await mountAuthPage(UnsubscribePage)
  await wrapper.find('[data-testid="unsubscribe-confirm-button"]').trigger('click')
  await flushPromises()
  return wrapper
}

describe('unsubscribe page', () => {
  it('sends nothing when it opens, only asks for confirmation', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })

    const wrapper = await mountAuthPage(UnsubscribePage)
    await flushPromises()

    expect(unsubscribe).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="unsubscribe-confirm"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="unsubscribe-failed"]').exists()).toBe(false)
  })

  it('sends the token of the link once the reader confirms, then shows the done panel', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    unsubscribe.mockResolvedValue('')

    const wrapper = await mountAndConfirm()

    expect(unsubscribe).toHaveBeenCalledTimes(1)
    expect(unsubscribe).toHaveBeenCalledWith('tok')
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="unsubscribe-confirm"]').exists()).toBe(false)
  })

  it('shows the API sentence when the link is refused', async () => {
    stubAuthRuntime({ query: { token: 'bad' } })
    unsubscribe.mockRejectedValue({ data: { detail: 'This link is not valid.' } })

    const wrapper = await mountAndConfirm()

    expect(wrapper.find('[data-testid="unsubscribe-failed"]').text()).toContain('This link is not valid.')
    expect(wrapper.find('[data-testid="unsubscribe-done"]').exists()).toBe(false)
  })

  it('asks the API about a link without a token, so its sentence is the one shown', async () => {
    stubAuthRuntime({ query: {} })
    unsubscribe.mockRejectedValue({ data: { detail: 'A token is required.' } })

    const wrapper = await mountAndConfirm()

    expect(unsubscribe).toHaveBeenCalledWith('')
    expect(wrapper.find('[data-testid="unsubscribe-failed"]').text()).toContain('A token is required.')
  })

  it('disables the button and shows the working line while the request is open', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    unsubscribe.mockReturnValue(new Promise(() => undefined))

    const wrapper = await mountAuthPage(UnsubscribePage)
    await wrapper.find('[data-testid="unsubscribe-confirm-button"]').trigger('click')

    const button = wrapper.find('[data-testid="unsubscribe-confirm-button"]')
    expect(button.attributes('disabled')).toBeDefined()
    expect(button.text()).toContain('client.unsubscribe.working')
  })

  it('keeps itself out of search results', async () => {
    stubAuthRuntime({ query: { token: 'tok' } })
    unsubscribe.mockResolvedValue('')

    await mountAuthPage(UnsubscribePage)

    expect(useSeoMeta).toHaveBeenCalledWith({ robots: 'noindex, nofollow' })
  })
})
