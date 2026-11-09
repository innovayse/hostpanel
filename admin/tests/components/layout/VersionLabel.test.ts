/**
 * Mount tests for VersionLabel: draws "Hostpanel <version>" from the version store, draws nothing
 * while the version is unknown, and every label on the page reads the same value without any
 * request of its own.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { h } from 'vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import { useVersionStore } from '@/stores/version'
import { json, stubFetch } from '../../helpers/fetch'
import { mountScreen } from '../../helpers/mount'

/** Two labels in one app, as the sidebar and the Settings page are. */
const TwoLabels = {
  render: () => [h(VersionLabel, { testid: 'first' }), h(VersionLabel, { testid: 'second' })],
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('VersionLabel', () => {
  it('shows the installed version once the store holds it', async () => {
    const { wrapper } = await mountScreen(VersionLabel)
    useVersionStore().version = '1.4.0'
    await flushPromises()

    expect(wrapper.get('[data-testid="version-label"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('uses the test id it is given', async () => {
    const { wrapper } = await mountScreen(VersionLabel, { testid: 'sidebar-version' })
    useVersionStore().version = '1.4.0'
    await flushPromises()

    expect(wrapper.get('[data-testid="sidebar-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('draws nothing while the version is unknown', async () => {
    const { wrapper } = await mountScreen(VersionLabel)

    expect(wrapper.find('[data-testid="version-label"]').exists()).toBe(false)
    expect(wrapper.text()).toBe('')
  })

  it('shows the same value in several labels and sends no request of its own', async () => {
    const mock = stubFetch(json({ installedVersion: '9.9.9' }))
    const { wrapper } = await mountScreen(TwoLabels)
    useVersionStore().version = '1.4.0'
    await flushPromises()

    expect(mock).not.toHaveBeenCalled()
    expect(wrapper.get('[data-testid="first"]').text()).toBe('Hostpanel 1.4.0')
    expect(wrapper.get('[data-testid="second"]').text()).toBe('Hostpanel 1.4.0')
  })
})
