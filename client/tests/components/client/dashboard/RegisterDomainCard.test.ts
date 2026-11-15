/**
 * Unit tests for ClientDashboardRegisterDomainCard: carrying the typed name to the domain search
 * or the billing portal's transfer form.
 *
 * @module tests/components/client/dashboard/RegisterDomainCard.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import RegisterDomainCard from '~/components/client/dashboard/RegisterDomainCard.vue'

beforeEach(() => {
  vi.mocked(navigateTo).mockClear()
  vi.stubGlobal('useRuntimeConfig', () => ({ public: {} }))
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('ClientDashboardRegisterDomainCard', () => {
  it('has a placeholder on the name field', () => {
    const wrapper = mountClient(RegisterDomainCard)

    expect(wrapper.find('[data-testid="dashboard-domain-query"]').attributes('placeholder')).toBe('client.dashboard.domainPlaceholder')
  })

  it('opens the domain search with the typed name', async () => {
    const wrapper = mountClient(RegisterDomainCard)

    await wrapper.find('[data-testid="dashboard-domain-query"]').setValue('  my site.com ')
    await wrapper.find('[data-testid="dashboard-domain-register"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith(`/domains?${DOMAIN_SEARCH_QUERY_PARAM}=my%20site.com`)
  })

  it('opens the bare domain search when nothing was typed', async () => {
    const wrapper = mountClient(RegisterDomainCard)

    await wrapper.find('[data-testid="dashboard-domain-register"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith('/domains')
  })

  it('registers on Enter in the name field', async () => {
    const wrapper = mountClient(RegisterDomainCard)

    await wrapper.find('[data-testid="dashboard-domain-query"]').setValue('a.com')
    await wrapper.find('[data-testid="dashboard-domain-query"]').trigger('keydown', { key: 'Enter' })

    expect(navigateTo).toHaveBeenCalledWith(`/domains?${DOMAIN_SEARCH_QUERY_PARAM}=a.com`)
  })

  it('sends a transfer to the billing portal when one is configured', async () => {
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { billingPortalUrl: 'https://billing.example.test' } }))
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    const wrapper = mountClient(RegisterDomainCard)

    await wrapper.find('[data-testid="dashboard-domain-query"]').setValue('a.com')
    await wrapper.find('[data-testid="dashboard-domain-transfer"]').trigger('click')

    expect(open).toHaveBeenCalledWith('https://billing.example.test/cart.php?a=add&domain=transfer&query=a.com', '_blank')
  })

  it('falls back to the domain search for a transfer without a portal or a name', async () => {
    const wrapper = mountClient(RegisterDomainCard)

    await wrapper.find('[data-testid="dashboard-domain-transfer"]').trigger('click')

    expect(navigateTo).toHaveBeenCalledWith('/domains')
  })
})
