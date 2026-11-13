/**
 * Mount tests for DemoAccountList: one row per published account with its role badge and credentials; a "Use" button on
 * Admin rows only, and for Client rows a portal link, or no row at all when no storefront address is configured.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import DemoAccountList from '@/components/auth/DemoAccountList.vue'
import { DemoAccountRole } from '@/types/demo'
import type { DemoAccount } from '@/types/demo'
import { createTestI18n } from '../../helpers/i18n'

/** Two accounts, one per role. */
const accounts: DemoAccount[] = [
  { role: DemoAccountRole.Admin, email: 'a@x.test', password: 'a-pw' },
  { role: DemoAccountRole.Client, email: 'c@x.test', password: 'c-pw' },
]

/**
 * Sets the storefront address the deployment would inject through .
 *
 * @param clientBaseUrl - Storefront origin; empty when the deployment publishes none.
 */
const stubClientBaseUrl = (clientBaseUrl: string): void => {
  vi.stubGlobal('__APP_CONFIG__', { clientBaseUrl })
}

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Mounts the list with the given accounts.
 *
 * @param list - The accounts to draw.
 * @returns The wrapper.
 */
const mountList = (list: DemoAccount[]) => mount(DemoAccountList, {
  props: { accounts: list },
  global: { plugins: [createTestI18n()] },
})

describe('DemoAccountList', () => {
  it('draws a row per account with the role label and credentials', () => {
    stubClientBaseUrl('https://shop.example.test')
    const wrapper = mountList(accounts)

    const admin = wrapper.get('[data-testid="demo-login-account-Admin"]').text()
    expect(admin).toContain('Admin')
    expect(admin).toContain('a@x.test')
    expect(admin).toContain('a-pw')
    expect(wrapper.get('[data-testid="demo-login-account-Client"]').text()).toContain('Client')
  })

  it('emits the Admin account when its button is pressed', async () => {
    stubClientBaseUrl('https://shop.example.test')
    const wrapper = mountList(accounts)

    await wrapper.get('[data-testid="demo-login-account-Admin"] [data-testid="demo-login-use"]').trigger('click')

    expect(wrapper.emitted('use')).toEqual([[accounts[0]]])
  })

  it('gives a Client row a portal link and no Use button', () => {
    stubClientBaseUrl('https://shop.example.test')
    const wrapper = mountList(accounts)

    const client = wrapper.get('[data-testid="demo-login-account-Client"]')
    expect(client.find('[data-testid="demo-login-use"]').exists()).toBe(false)
    expect(client.get('[data-testid="demo-login-client-portal"]').attributes('href')).toBe('https://shop.example.test')
    expect(wrapper.findAll('[data-testid="demo-login-use"]')).toHaveLength(1)
  })

  it('leaves Client accounts out when no storefront address is configured', () => {
    stubClientBaseUrl('')
    const wrapper = mountList(accounts)

    expect(wrapper.find('[data-testid="demo-login-account-Client"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="demo-login-account-Admin"]').exists()).toBe(true)
  })
})
