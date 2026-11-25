/**
 * Unit tests for ServicePanelLoginDialog: the panel's sign-in page is a link that opens in a new tab, the account's user name,
 * domain and server are listed, and no password and no form is ever shown.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import ServicePanelLoginDialog from '~/components/client/services/ServicePanelLoginDialog.vue'
import type { PanelLoginDetails } from '~/types/panellogindetails'

/** The details of an account whose panel reports a domain. */
const DETAILS: PanelLoginDetails = {
  username: 'ada',
  domain: 'a.test',
  serverAddress: '203.0.113.9',
  loginUrl: 'https://panel.example:8083/login/',
}

describe('ServicePanelLoginDialog', () => {
  it('links the sign-in page in a new tab and lists the account details', () => {
    const wrapper = mountClient(ServicePanelLoginDialog, { props: { details: DETAILS } })

    const link = wrapper.get('[data-testid="service-panel-login-link"]')
    expect(link.attributes('href')).toBe('https://panel.example:8083/login/')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener noreferrer')
    expect(wrapper.get('[data-testid="service-panel-login-username"]').text()).toContain('ada')
    expect(wrapper.get('[data-testid="service-panel-login-domain"]').text()).toContain('a.test')
    expect(wrapper.get('[data-testid="service-panel-login-server"]').text()).toContain('203.0.113.9')
  })

  it('shows no password field and no form, and says the password is not shown', () => {
    const wrapper = mountClient(ServicePanelLoginDialog, { props: { details: DETAILS } })

    expect(wrapper.find('input').exists()).toBe(false)
    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.text()).toContain('client.services.panelLoginNoPassword')
  })

  it('leaves the domain out when the panel reports none', () => {
    const wrapper = mountClient(ServicePanelLoginDialog, { props: { details: { ...DETAILS, domain: '' } } })

    expect(wrapper.find('[data-testid="service-panel-login-domain"]').exists()).toBe(false)
  })

  it('raises close when the dialog is closed', async () => {
    const wrapper = mountClient(ServicePanelLoginDialog, { props: { details: DETAILS } })

    await wrapper.get('[data-testid="ui-dialog-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
