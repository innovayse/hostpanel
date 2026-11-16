/**
 * Unit tests for PluginInstalledOverview: the version, category, capabilities, host range, refusal and release
 * notes are read from the installed plugin alone.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginInstalledOverview from '@/components/plugins/details/PluginInstalledOverview.vue'
import { PluginCapability, PluginManifestRule } from '@/types/plugin'
import { createTestI18n } from '../../../helpers/i18n'
import { makeInstalled } from '../../../helpers/pluginDetails'

/**
 * Mounts the overview.
 *
 * @param plugin - Fields of the installed plugin to change.
 * @returns The wrapper.
 */
const mountOverview = (plugin: Parameters<typeof makeInstalled>[0] = {}) =>
  mount(PluginInstalledOverview, { props: { plugin: makeInstalled(plugin) }, global: { plugins: [createTestI18n()] } })

describe('PluginInstalledOverview', () => {
  it('shows the version and the category of the installed plugin', () => {
    const wrapper = mountOverview()

    expect(wrapper.get('[data-testid="plugin-details-overview-version"]').text()).toContain('2.0.0')
    expect(wrapper.get('[data-testid="plugin-details-overview-category"]').text()).toContain('Payments')
  })

  it('lists the capabilities the manifest declares, and none when it declares none', () => {
    const none = mountOverview()
    const some = mountOverview({ capabilities: [PluginCapability.Endpoints] })

    expect(none.find('[data-testid="plugin-details-capabilities"]').exists()).toBe(false)
    expect(some.find(`[data-testid="plugin-details-capability-${PluginCapability.Endpoints}"]`).exists()).toBe(true)
  })

  it.each([
    [{ minHostVersion: '1.0.0', maxHostVersion: '2.0.0' }, '1.0.0 to 2.0.0'],
    [{ minHostVersion: '1.0.0', maxHostVersion: null }, '1.0.0 or newer'],
    [{ minHostVersion: null, maxHostVersion: '2.0.0' }, '2.0.0 or older'],
    [{ minHostVersion: null, maxHostVersion: null }, 'any host version'],
  ])('words the host range %j', (range, expected) => {
    const wrapper = mountOverview(range)

    expect(wrapper.get('[data-testid="plugin-details-overview-hosts"]').text()).toContain(expected)
  })

  it('shows the broken rule of a refused plugin', () => {
    const wrapper = mountOverview({ refusedRule: PluginManifestRule.CapabilityNotAllowed, refusedDetail: 'tickets' })

    expect(wrapper.get('[data-testid="plugin-details-refusal"]').text()).toContain('tickets')
  })

  it('draws the release notes as Markdown with links left as text, or says there are none', () => {
    const noted = mountOverview({ changelog: '- Faster **refunds**, see [notes](https://example.com/notes)' })
    const bare = mountOverview()
    const notes = noted.get('[data-testid="plugin-details-changelog"]')

    expect(notes.find('strong').text()).toBe('refunds')
    expect(notes.find('a').exists()).toBe(false)
    expect(notes.text()).toContain('notes')
    expect(bare.get('[data-testid="plugin-details-changelog"]').text()).toBe('No release notes.')
  })
})
