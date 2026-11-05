/**
 * Mount tests for IntegrationBreadcrumb: the link back to the list and the integration name.
 */
import { describe, expect, it } from 'vitest'
import IntegrationBreadcrumb from '@/components/integrations/IntegrationBreadcrumb.vue'
import { mountWithApp } from '../../helpers/mount'

describe('IntegrationBreadcrumb', () => {
  it('links back to the integrations list and shows the name', () => {
    const wrapper = mountWithApp(IntegrationBreadcrumb, { props: { name: 'Bank Transfer' } })

    expect(wrapper.get('[data-testid="integration-breadcrumb-root"]').attributes('href')).toBe('/integrations')
    expect(wrapper.get('[data-testid="integration-breadcrumb-root"]').text()).toBe('Integrations')
    expect(wrapper.text()).toContain('Bank Transfer')
  })

  it('shows the plugin picture beside the name of a plugin integration only', () => {
    const plugin = mountWithApp(IntegrationBreadcrumb, { props: { name: 'Acme', isPlugin: true, iconUrl: '/icon.png' } })
    const builtIn = mountWithApp(IntegrationBreadcrumb, { props: { name: 'Bank Transfer' } })

    expect(plugin.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/icon.png')
    expect(builtIn.find('[data-testid="integration-breadcrumb-avatar"]').exists()).toBe(false)
  })
})
