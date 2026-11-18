/**
 * Unit tests for ProfileView: read-only details, dashes for empty values, the language row.
 */
import { describe, expect, it } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import ProfileView from '~/components/account/ProfileView.vue'
import type { ClientUser } from '~/types/clientuser'
import { ClientVatStatus } from '~/types/clientvat'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { decodeEmailPrefs } from '~/utils/emailPreferences'

/** A profile with a company and an address holding only its country. */
const user: ClientUser = makeClientUser({
  firstName: 'Jane',
  lastName: 'Doe',
  companyName: 'Acme',
  email: 'jane@example.com',
  address: { street: null, address2: null, city: null, state: null, postCode: null, country: 'AM' }
})

/**
 * Mounts the view with the given overrides.
 *
 * @param overrides - Props that replace the defaults.
 * @returns The wrapper.
 */
const mountView = (overrides: Record<string, unknown> = {}) =>
  mountAccount(ProfileView, {
    props: {
      user,
      paymentMethodLabel: 'Card',
      languageLabel: 'English',
      ownsLanguage: true,
      emailPrefs: decodeEmailPrefs(undefined),
      ...overrides
    }
  })

describe('ProfileView', () => {
  it('shows the identity values', () => {
    const text = mountView().text()

    expect(text).toContain('Jane')
    expect(text).toContain('Acme')
    expect(text).toContain('Card')
    expect(text).toContain('AM')
  })

  it('shows a dash for an empty address field', () => {
    expect(mountView().text()).toContain('—')
  })

  it('shows the VAT number with the verdict of its last check', () => {
    const valid = mountView({
      user: makeClientUser({ ...user, vatNumber: 'DE123456789', vat: { status: ClientVatStatus.Valid, checkedAt: null, name: null, address: null } })
    })
    const invalid = mountView({
      user: makeClientUser({ ...user, vatNumber: 'DE123456789', vat: { status: ClientVatStatus.Invalid, checkedAt: null, name: null, address: null } })
    })
    const unchecked = mountView({ user: makeClientUser({ ...user, vatNumber: 'DE123456789' }) })

    expect(valid.get('[data-testid="profile-view-vat"]').text()).toContain('DE123456789')
    expect(valid.get('[data-testid="profile-view-vat"]').text()).toContain('client.profile.vatStatus.Valid')
    expect(invalid.get('[data-testid="profile-view-vat"]').text()).toContain('client.profile.vatStatus.Invalid')
    expect(unchecked.get('[data-testid="profile-view-vat"]').text()).toContain('client.profile.vatStatus.none')
  })

  it('shows no VAT number row when none is recorded', () => {
    expect(mountView().find('[data-testid="profile-view-vat"]').exists()).toBe(false)
  })

  it('shows the language row when the deployment owns the language', () => {
    expect(mountView().text()).toContain('English')
  })

  it('hides the language row under SSO', () => {
    expect(mountView({ ownsLanguage: false }).text()).not.toContain('English')
  })

  it('renders the email preferences read-only', () => {
    const wrapper = mountView()

    expect(wrapper.find('[data-testid="account-email-prefs"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-pref-general"]').attributes('disabled')).toBeDefined()
  })
})
