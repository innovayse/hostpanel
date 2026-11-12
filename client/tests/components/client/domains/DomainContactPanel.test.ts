/**
 * Unit tests for DomainContactPanel: the registry record, pre-filling the registrant from the
 * profile or a contact, editing, cancelling and saving. The store, the API composables and the
 * transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainContactPanel from '~/components/client/domains/DomainContactPanel.vue'
import { ContactType } from '~/types/contacttype'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { HttpMethod } from '~/types/httpmethod'
import { DOMAIN_ID, DomainAction, domainPath } from '~/tests/helpers/domainPanelPaths'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The registry record the API returns. */
const whois = { registrar: 'Reg Ltd', registrant: 'Ada Lovelace', createdAt: '2026-01-01T00:00:00Z', expiresAt: '2027-01-01T00:00:00Z' }

/** The registrant contact the registrar holds, as the backend answers it. */
const registrant = {
  firstName: 'Margaret',
  lastName: 'Hamilton',
  organization: null,
  email: 'margaret@example.com',
  phone: '777',
  address1: '2 Apollo Way',
  address2: null,
  city: 'Boston',
  state: 'MA',
  postalCode: '02101',
  country: 'US'
}

/** A contact as the backend lists it. */
const contact = {
  id: 5,
  firstName: 'Grace',
  lastName: 'Hopper',
  companyName: 'Navy',
  email: 'grace@example.com',
  phone: '555',
  type: ContactType.General,
  address: { street: '1 Pier', address2: null, city: 'Arlington', state: null, postCode: '22201', country: 'US' },
  notifications: { general: true, invoice: true, support: true, product: true, domain: false, affiliate: false }
}

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/**
 * Reads the value of a contact field.
 *
 * @param wrapper - The mounted panel.
 * @param key - The field key.
 * @returns The field's current value.
 */
const field = (wrapper: ReturnType<typeof mountClient>, key: string): string =>
  (wrapper.find(`[data-testid="domain-contact-${key}"]`).element as HTMLInputElement).value

/**
 * The writes of the registrant contact the stub received.
 *
 * @returns The PUT requests to the WHOIS path, oldest first.
 */
const whoisSaves = () => requestsTo(stub.requests, domainPath(DomainAction.Whois)).filter(request => request.method === HttpMethod.Put)

/**
 * Mounts the panel and waits for the profile to pre-fill the form.
 *
 * @returns The wrapper.
 */
const mountPanel = async () => {
  const wrapper = mountClient(DomainContactPanel, { props: { domainId: DOMAIN_ID } })
  await waitUntil(() => expect(field(wrapper, 'firstName')).toBe('Linus'))
  return wrapper
}

beforeEach(async () => {
  // By default the registrar holds no contact (the unscripted contact path is the BFF's own 404), so the profile stands in.
  table = {
    [`GET ${domainPath(DomainAction.Whois)}`]: { body: whois },
    [`GET ${AccountEndpoint.Contacts}`]: { body: [contact] },
    [`GET ${AccountEndpoint.Me}`]: {
      body: makeClientUser({
        firstName: 'Linus',
        lastName: 'T',
        address: { street: null, address2: null, city: 'Helsinki', state: null, postCode: '00100', country: null }
      })
    },
  }
  stub = await startTransport(request => routeTable(table)(request))
})

afterEach(async () => {
  await stub.close()
})

describe('DomainContactPanel', () => {
  it('shows the registry record and pre-fills the form from the primary profile', async () => {
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="domain-whois-record"]').text()).toContain('Reg Ltd')
    expect(wrapper.find('[data-testid="domain-whois-record"]').text()).toContain('Ada Lovelace')
    expect(field(wrapper, 'city')).toBe('Helsinki')
    expect(field(wrapper, 'postalCode')).toBe('00100')
    expect(wrapper.find('[data-testid="ui-tab-Tech"]').exists()).toBe(false)
  })

  it('pre-fills the form from the registrar contact instead of the profile when it is available', async () => {
    table[`GET ${domainPath(DomainAction.Contact)}`] = { body: registrant }
    const wrapper = mountClient(DomainContactPanel, { props: { domainId: DOMAIN_ID } })

    await waitUntil(() => expect(field(wrapper, 'firstName')).toBe('Margaret'))
    expect(field(wrapper, 'city')).toBe('Boston')
    expect(field(wrapper, 'postalCode')).toBe('02101')
    expect(requestsTo(stub.requests, domainPath(DomainAction.Contact))).toHaveLength(1)
  })

  it('says the record is unavailable when the registry lookup fails, and still offers the form', async () => {
    table[`GET ${domainPath(DomainAction.Whois)}`] = refusal('No record.')
    const wrapper = mountClient(DomainContactPanel, { props: { domainId: DOMAIN_ID } })

    await waitUntil(() => expect(wrapper.find('[data-testid="domain-whois-unavailable"]').exists()).toBe(true))
    await waitUntil(() => expect(field(wrapper, 'firstName')).toBe('Linus'))
  })

  it('locks the fields while an existing contact is selected and unlocks them for custom data', async () => {
    const wrapper = await mountPanel()

    expect(wrapper.find('[data-testid="domain-contact-firstName"]').attributes('disabled')).toBeDefined()

    await wrapper.find('[data-testid="domain-contact-custom"]').trigger('click')

    expect(wrapper.find('[data-testid="domain-contact-firstName"]').attributes('disabled')).toBeUndefined()
  })

  it('restores the loaded values when Cancel is pressed', async () => {
    const wrapper = await mountPanel()
    await wrapper.find('[data-testid="domain-contact-custom"]').trigger('click')
    await wrapper.find('[data-testid="domain-contact-firstName"]').setValue('Edited')

    await wrapper.find('[data-testid="domain-contact-cancel"]').trigger('click')

    expect(field(wrapper, 'firstName')).toBe('Linus')
  })

  it('saves the registrant contact and confirms', async () => {
    table[`PUT ${domainPath(DomainAction.Whois)}`] = { body: {} }
    const wrapper = await mountPanel()

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(whoisSaves()).toHaveLength(1))
    expect(bodyOf(whoisSaves()[0])).toMatchObject({ contact: { firstName: 'Linus', lastName: 'T', city: 'Helsinki', postalCode: '00100' } })
    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-success"]').exists()).toBe(true))
  })

  it('shows the refusal when the save fails', async () => {
    table[`PUT ${domainPath(DomainAction.Whois)}`] = refusal('Invalid phone.')
    const wrapper = await mountPanel()

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Invalid phone.'))
  })
})
