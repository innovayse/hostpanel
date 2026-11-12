/**
 * Unit tests for TeamTab: it shows the stored team and re-reads it after a change. The account
 * store, the client API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountAccount } from '~/tests/helpers/mountAccount'
import TeamTab from '~/components/account/TeamTab.vue'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { grantPermissions } from '~/tests/helpers/grantPermissions'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { makeTeamMember } from '~/tests/helpers/teamMember'
import { useAccountStore } from '~/stores/account'
import { HttpMethod } from '~/types/httpmethod'
import { Permission } from '~/types/permission'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

beforeEach(async () => {
  table = {
    [`${HttpMethod.Get} ${AccountEndpoint.Users}`]: { body: [makeTeamMember({ firstName: 'Helper', lastName: 'Member' })] },
    [`${HttpMethod.Post} ${AccountEndpoint.InviteUser}`]: { body: {} }
  }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('TeamTab', () => {
  it('renders the list and the invitation form', () => {
    const wrapper = mountAccount(TeamTab)

    expect(wrapper.find('[data-testid="account-team-list"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-invite-card"]').exists()).toBe(true)
  })

  it('lists the members the API returned', async () => {
    const wrapper = mountAccount(TeamTab)
    await grantPermissions(Permission.All)

    await useAccountStore().loadUsers()
    await wrapper.vm.$nextTick()

    expect(stub.requests.map(request => `${request.method} ${request.url}`)).toEqual([`${HttpMethod.Get} ${AccountEndpoint.Users}`])
    expect(wrapper.find('[data-testid="account-team-member-2"]').exists()).toBe(true)
  })

  it('reads the team again after an invitation went out', async () => {
    const wrapper = mountAccount(TeamTab)
    await grantPermissions(Permission.All)

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="account-team-member-2"]').exists()).toBe(true))
    expect(stub.requests.map(request => `${request.method} ${request.url}`)).toEqual([
      `${HttpMethod.Post} ${AccountEndpoint.InviteUser}`,
      `${HttpMethod.Get} ${AccountEndpoint.Users}`
    ])
  })
})
