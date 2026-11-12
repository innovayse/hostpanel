/**
 * Tests for `pages/client/domains/index.vue` against the real client store, the real client API
 * composable and the real transport, with a stub BFF answering on a real socket (nothing of ours
 * is mocked): the table, the tabs, the search, the paging and the expiring-soon badge.
 *
 * @module tests/pages/client/domains/index.test
 */

import type { VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { stubClientRuntime } from '~/tests/helpers/mountClient'
import { mountRenderedClientPage } from '~/tests/helpers/renderedPage'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import DomainsPage from '~/pages/client/domains/index.vue'
import { DomainStatus } from '~/types/domainstatus'
import { Permission } from '~/types/permission'

/** The BFF paths the page reads. */
const PATHS = {
  me: '/api/portal/client/me',
  domains: '/api/portal/client/domains',
} as const

/** The `data-testid` of an element the page always renders once its read has ended. */
const PAGE_MARKER = 'domains-register-new'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test fills the domains before it mounts. */
let table: StubRouteTable

/**
 * Builds a domain as the BFF answers it, with the fields the table reads.
 *
 * @param id - Domain id.
 * @param status - Domain status.
 * @param expiresAt - Expiry date.
 * @returns A domain.
 */
const domain = (id: number, status: DomainStatus, expiresAt = '2099-01-01T00:00:00Z') =>
  ({ id, name: `site${id}.com`, status, registeredAt: '2025-01-01', expiresAt })

/**
 * Lists the names of the domains the table shows.
 *
 * @param wrapper - The mounted page.
 * @returns The names, in order.
 */
const names = (wrapper: VueWrapper): string[] =>
  wrapper.findAll('[data-testid^="domain-row-"]').map((row) => row.find('td').text())

beforeEach(async () => {
  stubClientRuntime()
  table = { [`GET ${PATHS.me}`]: { body: makeClientUser({ permissions: Permission.All }) } }
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('domain list page', () => {
  it('lists the domains with a manage link each', async () => {
    table[`GET ${PATHS.domains}`] = { body: [domain(1, DomainStatus.Active), domain(2, DomainStatus.Expired)] }

    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    expect(names(wrapper)).toEqual(['site1.com', 'site2.com'])
    expect(wrapper.find('[data-testid="domain-manage-2"]').attributes('href')).toBe('/client/domains/2')
  })

  it('filters to the active and to the expired domains', async () => {
    table[`GET ${PATHS.domains}`] = { body: [domain(1, DomainStatus.Active), domain(2, DomainStatus.Expired), domain(3, DomainStatus.Active)] }
    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    await wrapper.find('[data-testid="ui-tab-current"]').trigger('click')
    expect(names(wrapper)).toEqual(['site1.com', 'site3.com'])

    await wrapper.find('[data-testid="ui-tab-finished"]').trigger('click')
    expect(names(wrapper)).toEqual(['site2.com'])
  })

  it('narrows the list by the search text, ignoring case', async () => {
    table[`GET ${PATHS.domains}`] = { body: [domain(1, DomainStatus.Active), domain(22, DomainStatus.Active)] }
    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    await wrapper.find('[data-testid="domains-search"]').setValue('SITE22')

    expect(names(wrapper)).toEqual(['site22.com'])
  })

  it('offers a search of new domains only when the list is empty without a query', async () => {
    table[`GET ${PATHS.domains}`] = { body: [] }
    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    expect(wrapper.find('[data-testid="domains-search-domains"]').attributes('href')).toBe('/domains')

    await wrapper.find('[data-testid="domains-search"]').setValue('nothing')
    expect(wrapper.find('[data-testid="domains-search-domains"]').exists()).toBe(false)
  })

  it('shows the first ten and pages the rest', async () => {
    table[`GET ${PATHS.domains}`] = { body: Array.from({ length: 12 }, (_, index) => domain(index + 1, DomainStatus.Active)) }

    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    expect(names(wrapper)).toHaveLength(10)
    expect(wrapper.find('[data-testid="ui-pagination-next"]').exists()).toBe(true)
  })

  it('flags a domain that expires within 30 days', async () => {
    const soon = new Date(Date.now() + 10 * 86_400_000).toISOString()
    table[`GET ${PATHS.domains}`] = { body: [domain(1, DomainStatus.Active, soon), domain(2, DomainStatus.Active)] }

    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    expect(wrapper.find('[data-testid="domain-row-1"]').text()).toContain('client.domains.expiringSoon')
    expect(wrapper.find('[data-testid="domain-row-2"]').text()).not.toContain('client.domains.expiringSoon')
  })

  it('shows the failure, not the empty state, when the read failed', async () => {
    table[`GET ${PATHS.domains}`] = { status: 502, body: { detail: 'Registrar is down.' } }

    const wrapper = await mountRenderedClientPage(DomainsPage, PAGE_MARKER)

    expect(wrapper.find('[data-testid="domains-error"]').text()).toBe('Registrar is down.')
  })
})
