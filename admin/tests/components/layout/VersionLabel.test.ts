/**
 * Mount tests for VersionLabel: draws "Hostpanel <version>" once the shared release status is
 * known, draws nothing while it loads or when the load fails, and shares one request between
 * several labels.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import { json, problem, stubFetch, stubOffline } from '../../helpers/fetch'
import { mountScreen } from '../../helpers/mount'

/** Release status as the API returns it. */
const dto = {
  installedVersion: '1.4.0',
  latestVersion: null,
  latestPublishedAt: null,
  releaseNotesUrl: null,
  upgradeCommand: null,
  isUpgradeAvailable: false,
}

/** Two labels in one app, as the sidebar and the Settings page are: they must share one request. */
const TwoLabels = {
  render: () => [h(VersionLabel, { testid: 'first' }), h(VersionLabel, { testid: 'second' })],
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('VersionLabel', () => {
  it('shows the installed version once it is loaded', async () => {
    stubFetch(json(dto))
    const { wrapper } = await mountScreen(VersionLabel)

    expect(wrapper.get('[data-testid="version-label"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('uses the test id it is given', async () => {
    stubFetch(json(dto))
    const { wrapper } = await mountScreen(VersionLabel, { testid: 'sidebar-version' })

    expect(wrapper.get('[data-testid="sidebar-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('draws nothing when the API refuses', async () => {
    stubFetch(problem('No access.', 403))
    const { wrapper } = await mountScreen(VersionLabel)

    expect(wrapper.find('[data-testid="version-label"]').exists()).toBe(false)
    expect(wrapper.text()).toBe('')
  })

  it('draws nothing when the API cannot be reached', async () => {
    stubOffline()
    const { wrapper } = await mountScreen(VersionLabel)

    expect(wrapper.find('[data-testid="version-label"]').exists()).toBe(false)
  })

  it('draws nothing while the version is still loading', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(VersionLabel)
    await flushPromises()

    expect(wrapper.find('[data-testid="version-label"]').exists()).toBe(false)
  })

  it('starts the shared load only once for several labels', async () => {
    const mock = stubFetch(json(dto))
    const { wrapper } = await mountScreen(TwoLabels)
    await flushPromises()

    expect(mock).toHaveBeenCalledTimes(1)
    expect(wrapper.get('[data-testid="first"]').text()).toBe('Hostpanel 1.4.0')
    expect(wrapper.get('[data-testid="second"]').text()).toBe('Hostpanel 1.4.0')
  })
})
