/**
 * Unit tests for InviteUserForm: the permission modes, the payload and the reset after success. The
 * account store, the client API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAccount } from '~/tests/helpers/mountAccount'
import InviteUserForm from '~/components/account/InviteUserForm.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { HttpMethod } from '~/types/httpmethod'
import { InvitePermissionMode } from '~/types/invitepermissionmode'
import { TeamPermission } from '~/types/teampermission'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

beforeEach(async () => {
  table = { [`${HttpMethod.Post} ${AccountEndpoint.InviteUser}`]: { body: {} } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

/**
 * Fills the three identity fields.
 *
 * @param wrapper - The mounted form.
 */
const fill = async (wrapper: ReturnType<typeof mountAccount>): Promise<void> => {
  await wrapper.find('[data-testid="account-invite-firstname"]').setValue('Jane')
  await wrapper.find('[data-testid="account-invite-lastname"]').setValue('Doe')
  await wrapper.find('[data-testid="account-invite-email"]').setValue('jane@example.com')
}

describe('InviteUserForm', () => {
  it('gives every field a placeholder', () => {
    const wrapper = mountAccount(InviteUserForm)

    for (const input of wrapper.findAll('input[type="text"], input[type="email"]')) {
      expect(input.attributes('placeholder')).toBeTruthy()
    }
  })

  it('hides the permission list until "choose" is selected', async () => {
    const wrapper = mountAccount(InviteUserForm)
    expect(wrapper.find('[data-testid="account-invite-perms"]').exists()).toBe(false)

    await wrapper.find('[data-testid="account-invite-perm-choose"]').trigger('click')

    expect(wrapper.find('[data-testid="account-invite-perms"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="account-invite-perms"] [role="checkbox"]')).toHaveLength(13)
  })

  it('sends "all" permissions by default', async () => {
    const wrapper = mountAccount(InviteUserForm)

    await fill(wrapper)
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(requestsTo(stub.requests, AccountEndpoint.InviteUser)).toHaveLength(1))
    const sent = stub.requests[0]
    expect(sent?.method).toBe(HttpMethod.Post)
    expect(bodyOf(sent)).toEqual({
      email: 'jane@example.com', firstName: 'Jane', lastName: 'Doe', permissionMode: InvitePermissionMode.All, permissions: []
    })
  })

  it('sends only the ticked permissions when choosing', async () => {
    const wrapper = mountAccount(InviteUserForm)

    await fill(wrapper)
    await wrapper.find('[data-testid="account-invite-perm-choose"]').trigger('click')
    await wrapper.find(`[data-testid="account-invite-perm-${TeamPermission.Tickets}"]`).trigger('click')
    await wrapper.find(`[data-testid="account-invite-perm-${TeamPermission.Invoices}"]`).trigger('click')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(requestsTo(stub.requests, AccountEndpoint.InviteUser)).toHaveLength(1))
    expect(bodyOf(stub.requests[0])).toMatchObject({
      permissionMode: InvitePermissionMode.Choose,
      permissions: [TeamPermission.Invoices, TeamPermission.Tickets]
    })
  })

  it('empties the form, reports success and asks for a fresh list', async () => {
    const wrapper = mountAccount(InviteUserForm)

    await fill(wrapper)
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.emitted('invited')).toHaveLength(1))
    await flushPromises()
    expect((wrapper.find('[data-testid="account-invite-email"]').element as HTMLInputElement).value).toBe('')
    expect(wrapper.find('[data-testid="ui-form-success"]').text()).toBe('client.users.inviteSent')
  })

  it('keeps the form and emits nothing when the invitation is refused', async () => {
    table[`${HttpMethod.Post} ${AccountEndpoint.InviteUser}`] = refusal('Already invited.')
    const wrapper = mountAccount(InviteUserForm)

    await fill(wrapper)
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toBe('Already invited.'))
    expect((wrapper.find('[data-testid="account-invite-email"]').element as HTMLInputElement).value).toBe('jane@example.com')
    expect(wrapper.emitted('invited')).toBeUndefined()
  })
})
