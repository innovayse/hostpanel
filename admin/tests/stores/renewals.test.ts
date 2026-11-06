/**
 * Tests for the renewals store: a load fills the three lists for the chosen window, the window reaches the API
 * as a query value, the auto-renew switch and the claim release send what the endpoints take, a manual run
 * words its outcome, and a refusal lands in `error` or `actionError` as the API sentence.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { RenewalNotice, useRenewalsStore } from '@/stores/renewals'
import { RenewalWindow } from '@/types/renewal'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/** An empty page of any list. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }

/** One service renewing soon. */
const service = {
  id: 4, clientId: 9, clientName: 'Ada Lovelace', productName: 'Starter', domain: null, amount: 5,
  priceCurrency: 'USD', billingCycle: 'Monthly', nextRenewalAt: '2027-01-10T00:00:00Z', autoRenew: true,
}

/** One domain expiring soon. */
const domain = {
  id: 7, clientId: 9, clientName: 'Ada Lovelace', name: 'example.test', regPeriodYears: 1, registrar: null,
  recurringAmount: 12, priceCurrency: 'USD', nextDueDate: '2027-01-12T00:00:00Z', expiresAt: '2027-01-12T00:00:00Z',
  status: 'Active', autoRenew: true,
}

/** One claimed line. */
const claim = {
  invoiceId: 5, itemId: 11, clientId: 9, clientName: 'Ada Lovelace', registeredDomainId: 7, description: 'example.test renewal',
  periodStart: '2027-01-12T00:00:00Z', periodEnd: '2028-01-12T00:00:00Z', claimedAt: '2027-01-01T10:00:00Z',
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('renewalsStore', () => {
  it('fetchAll fills the three lists and sends the window', async () => {
    const mock = stubRoutes({
      'GET /api/services/renewals/upcoming': () => json({ ...emptyPage, items: [service], totalCount: 1 }),
      'GET /api/domains/renewals/upcoming': () => json({ ...emptyPage, items: [domain], totalCount: 1 }),
      'GET /api/billing/renewal-claims': () => json([claim]),
    })
    const store = useRenewalsStore()

    const ok = await store.setWindow(RenewalWindow.Next60Days)

    expect(ok).toBe(true)
    expect(store.services).toHaveLength(1)
    expect(store.domains).toHaveLength(1)
    expect(store.claims).toHaveLength(1)
    expect(store.loading).toBe(false)
    const urls = mock.mock.calls.map(call => String(call[0]))
    expect(urls).toContain('/api/services/renewals/upcoming?window=Next60Days&page=1&pageSize=20')
    expect(urls).toContain('/api/domains/renewals/upcoming?window=Next60Days&page=1&pageSize=20')
  })

  it('a refused load lands in error', async () => {
    stubRoutes({
      'GET /api/services/renewals/upcoming': () => problem('Not allowed.', 403),
      'GET /api/domains/renewals/upcoming': () => json(emptyPage),
      'GET /api/billing/renewal-claims': () => json([]),
    })
    const store = useRenewalsStore()

    expect(await store.fetchAll()).toBe(false)

    expect(store.error).toBe('Not allowed.')
  })

  it('keeps the chosen window in renewalWindow', async () => {
    stubRoutes({
      'GET /api/services/renewals/upcoming': () => json(emptyPage),
      'GET /api/domains/renewals/upcoming': () => json(emptyPage),
      'GET /api/billing/renewal-claims': () => json([]),
    })
    const store = useRenewalsStore()

    await store.setWindow(RenewalWindow.Next90Days)

    expect(store.renewalWindow).toBe(RenewalWindow.Next90Days)
  })

  it('setServiceAutoRenew sends value and updates the row', async () => {
    const mock = stubRoutes({
      'GET /api/services/renewals/upcoming': () => json({ ...emptyPage, items: [{ ...service }], totalCount: 1 }),
      'PUT /api/services/4/auto-renew': () => new Response(null, { status: 204 }),
    })
    const store = useRenewalsStore()
    await store.fetchServices(1)

    expect(await store.setServiceAutoRenew(4, false)).toBe(true)

    expect(store.services[0]?.autoRenew).toBe(false)
    const put = mock.mock.calls.find(call => String(call[0]).endsWith('/services/4/auto-renew'))
    expect(JSON.parse(String(((put as unknown[])[1] as RequestInit).body))).toEqual({ value: false })
  })

  it('a refused switch keeps the row and puts the sentence in actionError', async () => {
    stubRoutes({
      'GET /api/domains/renewals/upcoming': () => json({ ...emptyPage, items: [{ ...domain }], totalCount: 1 }),
      'PUT /api/domains/7/auto-renew': () => problem('The registrar refused.', 502),
    })
    const store = useRenewalsStore()
    await store.fetchDomains(1)

    expect(await store.setDomainAutoRenew(7, false)).toBe(false)

    expect(store.domains[0]?.autoRenew).toBe(true)
    expect(store.actionError).toBe('The registrar refused.')
  })

  it('generateInvoices words the count and the empty run', async () => {
    stubRoutes({ 'POST /api/billing/client/9/renewal-invoices': () => json(2) })
    const store = useRenewalsStore()

    expect(await store.generateInvoices(9)).toBe(true)
    expect(store.notice).toBe(RenewalNotice.InvoicesRaised)
    expect(store.noticeCount).toBe(2)

    stubRoutes({ 'POST /api/billing/client/9/renewal-invoices': () => json(0) })
    expect(await store.generateInvoices(9)).toBe(true)
    expect(store.notice).toBe(RenewalNotice.NothingDue)
  })

  it('releaseClaim posts to the line and reloads the claims', async () => {
    const mock = stubRoutes({
      'POST /api/billing/5/items/11/release-renewal-claim': () => new Response(null, { status: 204 }),
      'GET /api/billing/renewal-claims': () => json([]),
    })
    const store = useRenewalsStore()
    store.claims = [claim]

    expect(await store.releaseClaim(5, 11)).toBe(true)

    expect(store.claims).toEqual([])
    expect(store.notice).toBe(RenewalNotice.ClaimReleased)
    expect(mock.mock.calls.map(call => String(call[0]))).toContain('/api/billing/renewal-claims')
  })

  it('a refused release keeps the sentence in actionError', async () => {
    stubRoutes({
      'POST /api/billing/5/items/11/release-renewal-claim': () => problem('This line has no renewal claim to release.', 409),
      'GET /api/billing/renewal-claims': () => json([claim]),
    })
    const store = useRenewalsStore()

    expect(await store.releaseClaim(5, 11)).toBe(false)

    expect(store.actionError).toBe('This line has no renewal claim to release.')
    expect(store.notice).toBeNull()
  })
})
