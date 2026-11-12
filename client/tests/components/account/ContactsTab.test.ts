/**
 * Unit tests for ContactsTab: states, opening the dialog and the confirmed deletion. The real
 * account and client stores, API composable and transport run against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import ContactsTab from '~/components/account/ContactsTab.vue'
import { useAccountStore } from '~/stores/account'
import { useClientStore } from '~/stores/client'
import { Permission } from '~/types/permission'
import { HttpMethod } from '~/types/httpmethod'
import type { AccountContact } from '~/types/accountcontact'
import { makeContact } from '~/tests/helpers/accountContact'
import { AccountEndpoint, contactPath } from '~/tests/helpers/accountPaths'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

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

/** One saved contact. */
const contact: AccountContact = makeContact({ id: 7, companyName: 'Acme' })

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** A refusal shaped like the API's problem body. */
const refused: StubReply = { status: 400, body: { detail: 'Refused.' } }

/**
 * The requests that reached the stub, as `METHOD path` lines.
 *
 * @returns The lines, oldest first.
 */
const wire = (): string[] => stub.requests.map(request => `${request.method} ${request.url}`)

/**
 * Mounts the tab for a person who may view and manage contacts, so the list is read again after a deletion.
 *
 * @returns The wrapper.
 */
const mountTab = () => {
  const wrapper = mountAccount(ContactsTab)
  useClientStore().user = makeClientUser({ permissions: Permission.All })
  return wrapper
}

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('ContactsTab', () => {
  it('shows a skeleton while loading', async () => {
    const wrapper = mountAccount(ContactsTab)
    useAccountStore().contactsLoading = true
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-contacts-loading"]').exists()).toBe(true)
  })

  it('shows the empty state without contacts', () => {
    const wrapper = mountAccount(ContactsTab)

    expect(wrapper.find('[data-testid="ui-empty-state"]').text()).toContain('client.contacts.empty')
  })

  it('lists the contacts of the store', async () => {
    const wrapper = mountAccount(ContactsTab)
    useAccountStore().contacts = [contact]
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-testid="account-contact-7"]').text()).toContain('Ann Lee')
    expect(wrapper.find('[data-testid="account-contact-7"]').text()).toContain('Acme')
  })

  it('opens the dialog empty from "add"', async () => {
    const wrapper = mountAccount(ContactsTab)

    await wrapper.find('[data-testid="account-contact-add"]').trigger('click')

    expect(wrapper.text()).toContain('client.contacts.addTitle')
  })

  it('opens the dialog filled from "edit"', async () => {
    const wrapper = mountAccount(ContactsTab)
    useAccountStore().contacts = [contact]
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-contact-edit-7"]').trigger('click')

    expect(wrapper.text()).toContain('client.contacts.editTitle')
    expect((wrapper.find('[data-testid="account-contact-firstname"]').element as HTMLInputElement).value).toBe('Ann')
  })

  it('deletes only after confirmation and then reads the list again', async () => {
    table[`${HttpMethod.Delete} ${contactPath(7)}`] = { body: {} }
    table[`${HttpMethod.Get} ${AccountEndpoint.Contacts}`] = { body: [] }
    const wrapper = mountTab()
    useAccountStore().contacts = [contact]
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-contact-delete-7"]').trigger('click')
    expect(stub.requests).toHaveLength(0)

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(wire()).toEqual([`DELETE ${contactPath(7)}`, `GET ${AccountEndpoint.Contacts}`]))
    await vi.waitFor(() => expect(wrapper.find('[data-testid="account-contact-7"]').exists()).toBe(false))
  })

  it('keeps the list when the deletion fails', async () => {
    table[`${HttpMethod.Delete} ${contactPath(7)}`] = refused
    const wrapper = mountTab()
    useAccountStore().contacts = [contact]
    await wrapper.vm.$nextTick()

    await wrapper.find('[data-testid="account-contact-delete-7"]').trigger('click')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(wire()).toEqual([`DELETE ${contactPath(7)}`]))
    expect(wrapper.find('[data-testid="account-contact-7"]').exists()).toBe(true)
  })
})
