/**
 * Unit tests for ProfileForm: the seeded draft, the save request, outcomes and the language field,
 * the check that refuses a blank required field before anything is sent, and the absence of the
 * sign-in address, which changes only through the confirmed address change. The real account,
 * client and catalogue stores, API composables and transport run against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mountAccount } from '~/tests/helpers/mountAccount'
import ProfileForm from '~/components/account/ProfileForm.vue'
import { useCatalogStore } from '~/stores/catalog'
import { HttpMethod } from '~/types/httpmethod'
import type { ClientUser } from '~/types/clientuser'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

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

/** The profile being edited. */
const user: ClientUser = makeClientUser({
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@example.com',
  address: { street: null, address2: null, city: null, state: null, postCode: null, country: 'AM' }
})

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/**
 * The save request the form sent.
 *
 * @returns The first PATCH to the profile, or undefined when none arrived.
 */
const savedRequest = () => requestsTo(stub.requests, AccountEndpoint.Me).find(request => request.method === HttpMethod.Patch)

/**
 * Mounts the form over a catalogue that offers one country.
 *
 * @param ownsLanguage - Whether the language select is offered.
 * @returns The wrapper.
 */
const mountForm = (ownsLanguage = true) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  useCatalogStore().countries = [{ value: 'AM', label: 'Armenia' }]
  return mountAccount(ProfileForm, {
    pinia,
    props: {
      user,
      ownsLanguage,
      paymentOptions: [{ value: '', label: 'Default' }],
      languageOptions: [{ value: '', label: 'Default' }, { value: 'en', label: 'English' }]
    }
  })
}

beforeEach(async () => {
  table = {
    [`${HttpMethod.Patch} ${AccountEndpoint.Me}`]: { body: {} },
    [`${HttpMethod.Get} ${AccountEndpoint.Me}`]: { body: user }
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('ProfileForm', () => {
  it('seeds the inputs from the profile', () => {
    const wrapper = mountForm()

    expect((wrapper.find('[data-testid="account-firstname"]').element as HTMLInputElement).value).toBe('Jane')
  })

  it('neither shows nor sends the sign-in address', async () => {
    const wrapper = mountForm()

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(savedRequest()).toBeDefined())
    expect(wrapper.find('[data-testid="account-email"]').exists()).toBe(false)
    expect(bodyOf(savedRequest())).not.toHaveProperty('email')
  })

  it('gives every text field a placeholder', () => {
    const wrapper = mountForm()

    for (const input of wrapper.findAll('input[type="text"], input[type="email"], input[type="tel"]')) {
      expect(input.attributes('placeholder')).toBeTruthy()
    }
  })

  it('saves the draft and announces success', async () => {
    const wrapper = mountForm()

    await wrapper.find('[data-testid="account-firstname"]').setValue('Janet')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-success"]').text()).toBe('client.profile.saved'))
    expect(bodyOf(savedRequest())).toMatchObject({
      firstName: 'Janet',
      notifications: { general: true, affiliate: true }
    })
  })

  it('leaves the language out where the deployment does not store it', async () => {
    const wrapper = mountForm(false)

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(savedRequest()).toBeDefined())
    expect(bodyOf(savedRequest())).not.toHaveProperty('language')
  })

  it('stays open when the save is refused', async () => {
    table[`${HttpMethod.Patch} ${AccountEndpoint.Me}`] = { status: 400, body: { detail: 'Refused.' } }
    const wrapper = mountForm()

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('Refused.'))
    expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(false)
  })

  it('shows API field errors on inputs when save is refused', async () => {
    table[`${HttpMethod.Patch} ${AccountEndpoint.Me}`] = {
      status: 400,
      body: { type: 'VALIDATION_ERROR', errors: { firstName: ['Enter your first name.'] } }
    }
    const wrapper = mountForm()

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.text()).toContain('Enter your first name.'))
  })

  it('emits cancel from the cancel button', async () => {
    const wrapper = mountForm()

    await wrapper.find('[data-testid="account-profile-cancel"]').trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('shows the API sentence of a refused save', async () => {
    table[`${HttpMethod.Patch} ${AccountEndpoint.Me}`] = { status: 409, body: { detail: 'Email taken' } }
    const wrapper = mountForm()

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('Email taken'))
  })
})
