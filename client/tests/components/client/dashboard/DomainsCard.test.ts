/**
 * Unit tests for ClientDashboardDomainsCard: the first domains, their most relevant date and
 * the card's states.
 *
 * @module tests/components/client/dashboard/DomainsCard.test
 */

import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountClient } from '~/tests/helpers/mountClient'
import DomainsCard from '~/components/client/dashboard/DomainsCard.vue'
import { useClientStore } from '~/stores/client'
import type { ClientDomain } from '~/types/clientdomain'
import { EMPTY_DATE } from '~/utils/formatDate'
import { DomainStatus } from '~/types/domainstatus'
import { clientDomainPath } from '~/constants/clientRoutes'

/**
 * Builds a domain row.
 *
 * @param fields - Fields to override.
 * @returns A domain with the given dates.
 */
const domain = (fields: Partial<ClientDomain> & { id: number }): ClientDomain =>
  ({ name: `d${fields.id}.test`, status: DomainStatus.Active, expiresAt: '', nextDueDate: '', registeredAt: '', ...fields }) as unknown as ClientDomain

describe('ClientDashboardDomainsCard', () => {
  it('shows skeletons while loading', async () => {
    const wrapper = mountClient(DomainsCard)
    useClientStore().domainsLoading = true
    await nextTick()

    expect(wrapper.findAll('[data-testid="ui-skeleton"]')).toHaveLength(2)
  })

  it('says there are no domains when the list is empty', () => {
    const wrapper = mountClient(DomainsCard)

    expect(wrapper.find('[data-testid="dashboard-domains-empty"]').text()).toContain('client.dashboard.noDomains')
  })

  it('lists at most four domains, each opening the domain', async () => {
    const wrapper = mountClient(DomainsCard)
    useClientStore().domains = [1, 2, 3, 4, 5].map((id) => domain({ id }))
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-domain-5"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-domain-1"]').attributes('href')).toBe(clientDomainPath(1))
    expect(wrapper.find('[data-testid="dashboard-domain-1"]').text()).toContain('d1.test')
  })

  it('labels the expiry date first, then the due date, then the registration date', async () => {
    const wrapper = mountClient(DomainsCard)
    useClientStore().domains = [
      domain({ id: 1, expiresAt: '2027-01-15', nextDueDate: '2026-12-01', registeredAt: '2020-01-01' }),
      domain({ id: 2, nextDueDate: '2026-12-01', registeredAt: '2020-01-01' }),
      domain({ id: 3, registeredAt: '2020-01-01' })
    ]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-domain-1"]').text()).toContain('client.dashboard.exp')
    expect(wrapper.find('[data-testid="dashboard-domain-2"]').text()).toContain('client.dashboard.due')
    expect(wrapper.find('[data-testid="dashboard-domain-3"]').text()).toContain('client.dashboard.reg')
  })

  it('shows a dash for a domain that carries no date at all', async () => {
    const wrapper = mountClient(DomainsCard)
    useClientStore().domains = [domain({ id: 1, expiresAt: '0001-01-01T00:00:00' })]
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-domain-1"]').text()).toContain(EMPTY_DATE)
  })
})
