/**
 * Tests for `composables/useDashboardSummary.ts` against a real client store: the lists behind
 * the dashboard banners and the de-duplicated load errors.
 *
 * @module tests/composables/useDashboardSummary.test
 */

import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useDashboardSummary } from '~/composables/useDashboardSummary'
import { EXPIRY_WARNING_DAYS } from '~/utils/domainExpiry'
import { useClientStore } from '~/stores/client'
import type { ClientDomain } from '~/types/clientdomain'
import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientService } from '~/types/clientservice'
import { InvoiceStatus } from '~/types/invoicestatus'

/** One day in milliseconds. */
const DAY_MS = 86_400_000

/**
 * Builds a domain that expires the given number of days from now.
 *
 * @param days - Days until expiry; negative for one already past.
 * @returns A domain row with only the fields the composable reads.
 */
const domainExpiringIn = (days: number): ClientDomain =>
  ({ id: days, name: `d${days}.test`, expiresAt: new Date(Date.now() + days * DAY_MS).toISOString() }) as unknown as ClientDomain

beforeEach(() => {
  setActivePinia(createPinia())
})

describe('useDashboardSummary.expiringDomains', () => {
  it('includes domains inside the warning window and past ones, and leaves later ones out', () => {
    const store = useClientStore()
    store.domains = [domainExpiringIn(10), domainExpiringIn(EXPIRY_WARNING_DAYS + 30), domainExpiringIn(-3)]

    const { expiringDomains } = useDashboardSummary()

    expect(expiringDomains.value.map((domain) => domain.id)).toEqual([10, -3])
  })

  it('ignores a domain that carries the zero date or no date at all', () => {
    const store = useClientStore()
    store.domains = [
      { id: 1, expiresAt: '0001-01-01T00:00:00' },
      { id: 2, expiresAt: '' }
    ] as unknown as ClientDomain[]

    const { expiringDomains } = useDashboardSummary()

    expect(expiringDomains.value).toEqual([])
  })
})

describe('useDashboardSummary.servicesNeedingSetup', () => {
  it('lists the services that have no control-panel username', () => {
    const store = useClientStore()
    store.services = [{ id: 1, username: '' }, { id: 2, username: 'ada' }] as unknown as ClientService[]

    const { servicesNeedingSetup } = useDashboardSummary()

    expect(servicesNeedingSetup.value.map((service) => service.id)).toEqual([1])
  })

  it('leaves out a digital service, which has no username and nothing to set up', () => {
    const store = useClientStore()
    store.services = [
      { id: 1, username: '', isDigital: false },
      { id: 2, username: null, isDigital: true }
    ] as unknown as ClientService[]

    const { servicesNeedingSetup } = useDashboardSummary()

    expect(servicesNeedingSetup.value.map((service) => service.id)).toEqual([1])
  })
})

describe('useDashboardSummary.unpaidInvoices', () => {
  it('counts unpaid and overdue invoices but not paid ones', () => {
    const store = useClientStore()
    store.invoices = [
      { id: 1, status: InvoiceStatus.Unpaid },
      { id: 2, status: InvoiceStatus.Paid },
      { id: 3, status: InvoiceStatus.Overdue }
    ] as unknown as ClientInvoice[]

    const { unpaidInvoices } = useDashboardSummary()

    expect(unpaidInvoices.value.map((invoice) => invoice.id)).toEqual([1, 3])
  })
})

describe('useDashboardSummary.loadErrors', () => {
  it('lists each distinct section failure once', () => {
    const store = useClientStore()
    store.servicesError = 'Refused.'
    store.invoicesError = 'Refused.'
    store.domainsError = 'Other.'

    const { loadErrors } = useDashboardSummary()

    expect(loadErrors.value).toEqual(['Refused.', 'Other.'])
  })

  it('is empty when nothing failed', () => {
    const { loadErrors } = useDashboardSummary()

    expect(loadErrors.value).toEqual([])
  })
})

describe('useDashboardSummary.statsLoading', () => {
  it('is true while any section is loading', () => {
    const store = useClientStore()
    const { statsLoading } = useDashboardSummary()
    expect(statsLoading.value).toBe(false)

    store.ticketsLoading = true

    expect(statsLoading.value).toBe(true)
  })
})
