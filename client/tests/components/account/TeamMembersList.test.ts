/**
 * Unit tests for TeamMembersList: states, the owner row and the confirmed removal. The account
 * store, the client API composable and the transport run for real against a stub BFF.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountAccount } from '~/tests/helpers/mountAccount'
import TeamMembersList from '~/components/account/TeamMembersList.vue'
import { userPath } from '~/tests/helpers/accountPaths'
import { routeTable, refusal } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { makeTeamMember } from '~/tests/helpers/teamMember'
import { useAccountStore } from '~/stores/account'
import { HttpMethod } from '~/types/httpmethod'
import type { TeamMember } from '~/types/teammember'

/** An owner and one removable member. */
const users: TeamMember[] = [
  makeTeamMember({
    userId: '1', firstName: 'Olive', lastName: 'Owner', email: 'owner@example.com', isOwner: true,
    permissions: 8191, lastLoginAt: '2026-01-01T00:00:00Z', createdAt: '2025-01-01T00:00:00Z'
  }),
  makeTeamMember()
]

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

beforeEach(async () => {
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.unstubAllGlobals()
  await stub.close()
})

describe('TeamMembersList', () => {
  it('shows a skeleton while loading', () => {
    const wrapper = mountAccount(TeamMembersList, { props: { users: [], loading: true } })

    expect(wrapper.find('[data-testid="account-team-loading"]').exists()).toBe(true)
  })

  it('shows the empty state without members', () => {
    const wrapper = mountAccount(TeamMembersList, { props: { users: [], loading: false } })

    expect(wrapper.find('[data-testid="account-team-empty"]').exists()).toBe(true)
  })

  it('lists members, the owner without a remove button', () => {
    const wrapper = mountAccount(TeamMembersList, { props: { users, loading: false } })

    expect(wrapper.find('[data-testid="account-team-member-1"]').text()).toContain('client.users.owner')
    expect(wrapper.find('[data-testid="account-team-remove-1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-team-remove-2"]').exists()).toBe(true)
  })

  it('removes a member only after confirmation, then asks for a fresh list', async () => {
    table[`${HttpMethod.Delete} ${userPath('2')}`] = { body: {} }
    const wrapper = mountAccount(TeamMembersList, { props: { users, loading: false } })

    await wrapper.find('[data-testid="account-team-remove-2"]').trigger('click')
    expect(stub.requests).toHaveLength(0)

    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(wrapper.emitted('changed')).toHaveLength(1))
    expect(stub.requests).toHaveLength(1)
    expect(stub.requests[0]?.method).toBe(HttpMethod.Delete)
    expect(stub.requests[0]?.url).toBe(userPath('2'))
  })

  it('does not ask for a fresh list when the removal is refused', async () => {
    table[`${HttpMethod.Delete} ${userPath('2')}`] = refusal('Cannot remove.')
    const wrapper = mountAccount(TeamMembersList, { props: { users, loading: false } })

    await wrapper.find('[data-testid="account-team-remove-2"]').trigger('click')
    await wrapper.find('[data-testid="ui-confirm-confirm"]').trigger('click')

    await vi.waitFor(() => expect(useAccountStore().inviteError).toBe('Cannot remove.'))
    await flushPromises()
    expect(stub.requests).toHaveLength(1)
    expect(wrapper.emitted('changed')).toBeUndefined()
  })
})
