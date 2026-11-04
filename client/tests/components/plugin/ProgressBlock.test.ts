/**
 * Tests for PluginProgressBlock: share of `max`, clamped, non-numbers as 0, a value from a route, a failed read.
 *
 * @module tests/components/plugin/ProgressBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.unstubAllGlobals()
})

/**
 * The value the bar was drawn with.
 *
 * @param wrapper - The mounted view.
 * @returns The bar's `aria-valuenow`.
 */
const shown = (wrapper: ReturnType<typeof mountBlocks>): string | undefined =>
  wrapper.get('[data-testid="plugin-progress"]').attributes('aria-valuenow')

describe('PluginProgressBlock', () => {
  it.each([
    [{ value: 5, max: 10 }, '50'],
    [{ value: 500 }, '100'],
    [{ value: -3 }, '0'],
    [{ value: 30, max: 0 }, '30'],
  ])('draws %o as %s percent', (data, expected) => {
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Progress, label: 'Build', ...data }])

    expect(shown(wrapper)).toBe(expected)
  })

  it('reads its value from the route, and a non-number draws 0', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { done: 'lots' } } })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Progress, label: 'Build', source: { route: TEST_ROUTES.site, field: 'done' } }])
    await settle()

    expect(shown(wrapper)).toBe('0')
  })

  it('shows the API\'s sentence when the read fails', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: problem(403, 'You may not see this.') })
    const wrapper = mountBlocks([{ kind: PluginBlockKind.Progress, label: 'Build', source: { route: TEST_ROUTES.site } }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-progress-error"]').text()).toBe('You may not see this.')
  })
})
