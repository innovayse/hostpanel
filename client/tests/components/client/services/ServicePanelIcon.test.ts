/**
 * Unit tests for ServicePanelIcon: the plugin's picture named after the plugin, and the generic icon when no
 * plugin serves the server, the plugin ships no icon, or the picture cannot be loaded.
 */
import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { Monitor } from 'lucide-vue-next'
import { mountClient } from '~/tests/helpers/mountClient'
import ServicePanelIcon from '~/components/client/services/ServicePanelIcon.vue'

/** A server whose module a plugin serves, with the given icon version (null when the plugin ships no icon). */
const served = (iconVersion: string | null) => ({
  server: { name: 'srv', hostname: 'srv.test', ipAddress: null, panelPlugin: { id: 'acme-panel', name: 'Acme Panel', iconVersion } },
})

describe('ServicePanelIcon', () => {
  it('draws the plugin icon from the portal route, named after the plugin', () => {
    const wrapper = mountClient(ServicePanelIcon, { props: { service: served('1.2.0'), fallback: Monitor } })

    const image = wrapper.get('[data-testid="service-panel-plugin-icon"]')
    expect(image.attributes('src')).toBe('/api/portal/client/plugins/acme-panel/icon?v=1.2.0')
    expect(image.attributes('alt')).toBe('Acme Panel')
  })

  it('draws the generic icon when the plugin ships no icon', () => {
    const wrapper = mountClient(ServicePanelIcon, { props: { service: served(null), fallback: Monitor } })

    expect(wrapper.find('[data-testid="service-panel-plugin-icon"]').exists()).toBe(false)
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('draws the generic icon when no plugin serves the server, or no server is known', () => {
    const unserved = mountClient(ServicePanelIcon, { props: { service: { server: { name: 's', hostname: 'h', ipAddress: null, panelPlugin: null } }, fallback: Monitor } })
    const unknown = mountClient(ServicePanelIcon, { props: { service: { server: null }, fallback: Monitor } })

    for (const wrapper of [unserved, unknown]) {
      expect(wrapper.find('[data-testid="service-panel-plugin-icon"]').exists()).toBe(false)
      expect(wrapper.find('svg').exists()).toBe(true)
    }
  })

  it('falls back to the generic icon when the picture cannot be loaded', async () => {
    const wrapper = mountClient(ServicePanelIcon, { props: { service: served('1.2.0'), fallback: Monitor } })

    await wrapper.get('[data-testid="service-panel-plugin-icon"]').trigger('error')
    await nextTick()

    expect(wrapper.find('[data-testid="service-panel-plugin-icon"]').exists()).toBe(false)
    expect(wrapper.find('svg').exists()).toBe(true)
  })
})
