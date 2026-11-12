/**
 * Unit tests for ContactDialog: add vs edit seeding, validation, saving and closing. The real
 * account store, API composable and transport run against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import ContactDialog from '~/components/account/ContactDialog.vue'
import { HttpMethod } from '~/types/httpmethod'
import type { AccountContact } from '~/types/accountcontact'
import { ContactType } from '~/types/contacttype'
import { makeContact } from '~/tests/helpers/accountContact'
import { AccountEndpoint, contactPath } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
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

/** The stub playing the BFF; it accepts every request. */
let stub: StubServer

beforeEach(async () => {
  stub = await startTransport(() => ({ body: {} }))
})

afterEach(async () => {
  await stub.close()
})

/** A contact with the support mail switched off. */
const contact: AccountContact = makeContact({
  id: 7, phone: '555', type: ContactType.Billing,
  address: { street: '1 Pier', address2: null, city: 'Arlington', state: null, postCode: null, country: null },
  notifications: { general: true, invoice: false, support: false, product: true, domain: true, affiliate: false }
})

describe('ContactDialog', () => {
  it('titles the dialog "add" without a contact and starts empty', () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact: null } })

    expect(wrapper.text()).toContain('client.contacts.addTitle')
    expect((wrapper.find('[data-testid="account-contact-firstname"]').element as HTMLInputElement).value).toBe('')
  })

  it('titles the dialog "edit" and seeds the draft from the contact', () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact } })

    expect(wrapper.text()).toContain('client.contacts.editTitle')
    expect((wrapper.find('[data-testid="account-contact-email"]').element as HTMLInputElement).value).toBe('ann@example.com')
    expect(wrapper.find('[data-testid="account-contact-invoice"]').attributes('aria-checked')).toBe('false')
  })

  it('gives every text field a placeholder', () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact: null } })

    for (const input of wrapper.findAll('input[type="text"], input[type="email"], input[type="tel"]')) {
      expect(input.attributes('placeholder')).toBeTruthy()
    }
  })

  it('creates when there is no contact, then closes and announces the save', async () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact: null } })

    await wrapper.find('[data-testid="account-contact-firstname"]').setValue('Ann')
    await wrapper.find('[data-testid="account-contact-lastname"]').setValue('Lee')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.emitted('saved')).toHaveLength(1))
    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([false])
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Post)
    expect(stub.requests[0]?.url).toBe(AccountEndpoint.Contacts)
    expect(bodyOf(stub.requests[0])).toMatchObject({ firstName: 'Ann', lastName: 'Lee', phone: null, address: { street: null } })
  })

  it('updates by id when editing', async () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact } })

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(stub.requests).toHaveLength(1))
    expect(stub.requests[0]?.method).toBe(HttpMethod.Put)
    expect(stub.requests[0]?.url).toBe(contactPath(7))
    expect(bodyOf(stub.requests[0])).toMatchObject({
      lastName: 'Lee', type: ContactType.Billing, companyName: null,
      notifications: { support: false },
      address: { street: '1 Pier', city: 'Arlington' }
    })
  })

  it('shows the local sentence when the name is missing and stays open', async () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact: null } })

    await wrapper.find('form').trigger('submit')
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('client.contacts.saveError')
    expect(wrapper.emitted('saved')).toBeUndefined()
    expect(stub.requests).toHaveLength(0)
  })

  it('closes from the cancel button without saving', async () => {
    const wrapper = mountAccount(ContactDialog, { props: { modelValue: true, contact: null } })

    await wrapper.find('[data-testid="account-contact-cancel"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([false])
    expect(wrapper.emitted('saved')).toBeUndefined()
  })
})
