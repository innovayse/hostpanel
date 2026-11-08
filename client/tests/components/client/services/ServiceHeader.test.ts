/**
 * Unit tests for ServiceHeader: the title block, the control-panel button's conditions and its event.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { Permission } from '~/types/permission'
import ServiceHeader from '~/components/client/services/ServiceHeader.vue'
import { makeClientService as service } from '~/tests/helpers/clientServiceFixture'
import { ServiceStatus } from '~/types/servicestatus'

describe('ServiceHeader', () => {
  it('shows the plan name and the domain', () => {
    const wrapper = mountClient(ServiceHeader, { props: { service: service(), ssoLoading: false } })

    expect(wrapper.find('h1').text()).toBe('Starter')
    expect(wrapper.text()).toContain('example.com')
  })

  it('says there is no domain when the service has none', () => {
    const wrapper = mountClient(ServiceHeader, { props: { service: service({ domain: null }), ssoLoading: false } })

    expect(wrapper.text()).toContain('client.dashboard.noDomain')
  })

  it('offers the control-panel button for an active service on a known server', () => {
    const wrapper = mountClient(ServiceHeader, { props: { service: service(), ssoLoading: false } })

    expect(wrapper.find('[data-testid="service-cpanel-login"]').exists()).toBe(true)
  })

  it.each([
    [{ status: ServiceStatus.Suspended }],
    [{ server: null }],
  ])('hides the control-panel button for %j', (overrides) => {
    const wrapper = mountClient(ServiceHeader, { props: { service: service(overrides), ssoLoading: false } })

    expect(wrapper.find('[data-testid="service-cpanel-login"]').exists()).toBe(false)
  })

  it('asks for the control panel when the button is clicked', async () => {
    const wrapper = mountClient(ServiceHeader, { props: { service: service(), ssoLoading: false } })

    await wrapper.find('[data-testid="service-cpanel-login"]').trigger('click')

    expect(wrapper.emitted('cpanel')).toHaveLength(1)
  })
})

describe('ServiceHeader permissions', () => {
  it('offers the control-panel button only to a person allowed single sign-on', async () => {
    const wrapper = mountClient(ServiceHeader, { props: { service: service(), ssoLoading: false } })

    await grantPermissions(Permission.ViewProductsServices)
    expect(wrapper.find('[data-testid="service-cpanel-login"]').exists()).toBe(false)

    await grantPermissions(Permission.ViewProductsServices | Permission.AllowSingleSignOn)
    expect(wrapper.find('[data-testid="service-cpanel-login"]').exists()).toBe(true)
  })
})
