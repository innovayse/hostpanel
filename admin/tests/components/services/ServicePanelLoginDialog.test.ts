/**
 * Mount tests for ServicePanelLoginDialog: the way to sign in by hand to a panel that cannot sign anybody in itself. The
 * panel's sign in page is a link that opens in a new tab, the account's user name, domain and server are listed, and no
 * password and no form is ever shown.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ServicePanelLoginDialog from '@/components/services/ServicePanelLoginDialog.vue'
import type { PanelLoginDetails } from '@/types/service'
import { createTestI18n } from '../../helpers/i18n'

/** The details of an account whose panel reports a domain. */
const details: PanelLoginDetails = {
  username: 'ada',
  domain: 'site.example',
  serverAddress: '203.0.113.9',
  loginUrl: 'https://panel.example:8083/login/',
}

/**
 * Mounts the dialog with the teleport inlined.
 *
 * @param value - The login details to show.
 * @returns The wrapper.
 */
const mountDialog = (value: PanelLoginDetails) =>
  mount(ServicePanelLoginDialog, { props: { details: value }, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

describe('ServicePanelLoginDialog', () => {
  it('links the panel sign in page in a new tab and lists the account details', () => {
    const wrapper = mountDialog(details)

    const link = wrapper.get('[data-testid="client-service-panel-login-link"]')
    expect(link.attributes('href')).toBe('https://panel.example:8083/login/')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toBe('noopener noreferrer')
    expect(wrapper.get('[data-testid="client-service-panel-login-username"]').text()).toBe('ada')
    expect(wrapper.get('[data-testid="client-service-panel-login-domain"]').text()).toBe('site.example')
    expect(wrapper.get('[data-testid="client-service-panel-login-server"]').text()).toBe('203.0.113.9')
  })

  it('shows no password and no form, and says the password is not shown', () => {
    const wrapper = mountDialog(details)

    expect(wrapper.find('input').exists()).toBe(false)
    expect(wrapper.find('form').exists()).toBe(false)
    expect(wrapper.text()).toContain('The password is not shown here')
  })

  it('leaves the domain out when the panel reports none', () => {
    const wrapper = mountDialog({ ...details, domain: '' })

    expect(wrapper.find('[data-testid="client-service-panel-login-domain"]').exists()).toBe(false)
  })

  it('raises close when the close button is pressed', async () => {
    const wrapper = mountDialog(details)

    await wrapper.get('[data-testid="client-service-panel-login-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
