/**
 * Unit tests for ProfileTab: loading, failure, the view, the edit switch and the sign-in address card.
 * The real stores and the real `useAuthMode` run; the deployment is a local-accounts one, so the
 * language and the sign-in address are offered.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { Permission } from '~/types/permission'
import { AuthMode } from '~/types/authmode'
import ProfileTab from '~/components/account/ProfileTab.vue'
import { useAuthMode } from '~/composables/useAuthMode'
import { useAccountStore } from '~/stores/account'
import { useClientStore } from '~/stores/client'
import type { ClientUser } from '~/types/clientuser'
import { makeClientUser } from '~/tests/helpers/clientUser'

vi.mock('qrcode', () => ({
  default: {
    /**
     * Stands in for the QR renderer, a third-party package the account store imports.
     *
     * @returns An empty data URL.
     */
    toDataURL: async (): Promise<string> => ''
  }
}))

beforeEach(() => {
  // Nuxt auto-imports the composable; the unit environment registers the real one under that name.
  vi.stubGlobal('useAuthMode', useAuthMode)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { authMode: AuthMode.Local } }))
  stubLocalePath()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A signed-in profile with a default gateway and a language. */
const user: ClientUser = makeClientUser({
  firstName: 'Jane', lastName: 'Doe', email: 'jane@example.com', permissions: Permission.All, paymentMethod: 'card_gateway', language: 'ru'
})

describe('ProfileTab', () => {
  it('shows a skeleton while the profile loads', () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().userLoading = true

    return wrapper.vm.$nextTick().then(() => {
      expect(wrapper.find('[data-testid="account-profile-loading"]').exists()).toBe(true)
    })
  })

  it('keeps the profile on screen while it is read again', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().user = user
    useClientStore().userLoading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-profile-loading"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-profile-edit"]').exists()).toBe(true)
  })

  it('says so when the profile could not be loaded', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().userLoading = false
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-profile-failed"]').exists()).toBe(true)
  })

  it('shows the details with the gateway and language named', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().user = user
    useClientStore().userLoading = false
    useAccountStore().gatewayMethods = [{ module: 'card_gateway', displayName: 'Credit card', supportsStoredCredentials: false }]
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-profile-view"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Credit card')
    expect(wrapper.text()).toContain('client.profile.langRussian')
  })

  it('switches to the form on edit and back on cancel', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().user = user
    useClientStore().userLoading = false
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-profile-edit"]').trigger('click')
    expect(wrapper.find('[data-testid="account-profile-form"]').exists()).toBe(true)

    await wrapper.find('[data-testid="account-profile-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="account-profile-view"]').exists()).toBe(true)
  })

  it('reports two-factor from the profile', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().user = { ...user, twoFactorEnabled: true }
    useClientStore().userLoading = false
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-2fa-badge"]').text()).toBe('client.twoFactor.enabled')
  })
})

describe('ProfileTab permissions', () => {
  it('shows the details read-only to a person who may not modify the master profile', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().user = { ...user, permissions: Permission.ViewDomains }
    useClientStore().userLoading = false
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-profile-view"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-profile-edit"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-email-change-card"]').exists()).toBe(false)
  })

  it('offers the edit button with ModifyMasterProfile', async () => {
    const wrapper = mountAccount(ProfileTab)
    useClientStore().user = { ...user, permissions: Permission.ModifyMasterProfile }
    useClientStore().userLoading = false
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-profile-edit"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-email-change-card"]').exists()).toBe(true)
  })
})
