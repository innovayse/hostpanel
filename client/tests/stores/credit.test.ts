/**
 * Tests for `stores/credit.ts`, through the real API composable and transport against a stub server.
 *
 * The store, not the credit pages, owns the loaded credit, the in-flight flags and the refusal sentences, so these assertions pin
 * the contract the pages render from.
 *
 * @module tests/stores/credit.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { startTransport } from '../helpers/stubTransport'
import { queryOf, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubReply, StubServer } from '../helpers/stubServer'
import { CREDIT_PAGE_SIZE, useCreditStore } from '~/stores/credit'
import type { ClientCredit } from '~/types/clientcredit'

/** The BFF path of the credit read. */
const CREDIT_PATH = '/api/portal/client/credit'

/** The BFF path of the top-up invoice creation. */
const TOP_UP_PATH = '/api/portal/client/credit/top-ups'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test, by `METHOD /path`. */
let table: StubRouteTable

/** A refusal as the BFF answers it. */
const refusal: StubReply = { status: 400, body: { detail: 'Refused.' } }

/**
 * Builds a credit.
 *
 * @param balance - The balance.
 * @returns The credit with an empty history.
 */
const credit = (balance: number): ClientCredit => ({
  balance,
  currency: 'USD',
  topUpMinimum: 5,
  topUpMaximum: 1000,
  entries: { items: [], pageNumber: 1, pageSize: CREDIT_PAGE_SIZE, totalCount: 0, totalPages: 0 }
})

/**
 * The page each credit read asked for.
 *
 * @returns The `page` and `pageSize` queries of the credit reads, oldest first.
 */
const creditReads = (): Record<string, string>[] => requestsTo(stub.requests, CREDIT_PATH).map(queryOf)

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
})

describe('useCreditStore.load', () => {
  it('remembers the page a failed read asked for, so a retry asks for that one', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(5) }
    const store = useCreditStore()
    await store.load(1)

    table[`GET ${CREDIT_PATH}`] = { status: 404, body: { detail: 'Gone.' } }
    await store.load(3)

    expect(store.error).toBe('Gone.')
    expect(store.page).toBe(1)
    expect(store.requestedPage).toBe(3)
  })

  it('reads the credit page and exposes the balance', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(12.5) }
    const store = useCreditStore()

    const loaded = await store.load(1)

    expect(creditReads()).toEqual([{ page: '1', pageSize: String(CREDIT_PAGE_SIZE) }])
    expect(loaded?.balance).toBe(12.5)
    expect(store.balance).toBe(12.5)
    expect(store.hasCredit).toBe(true)
    expect(store.loading).toBe(false)
    expect(store.error).toBe('')
  })

  it('remembers the page it read and asks for it again when no page is given', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(0) }
    const store = useCreditStore()

    await store.load(3)
    await store.load()

    expect(creditReads().at(-1)).toEqual({ page: '3', pageSize: String(CREDIT_PAGE_SIZE) })
    expect(store.page).toBe(3)
  })

  it('reads a zero balance as no credit to spend', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(0) }
    const store = useCreditStore()

    await store.load(1)

    expect(store.hasCredit).toBe(false)
  })

  it('keeps the sentence, drops the stale credit and releases the flag when the read fails', async () => {
    table[`GET ${CREDIT_PATH}`] = { body: credit(9) }
    const store = useCreditStore()
    await store.load(1)

    table[`GET ${CREDIT_PATH}`] = { status: 404, body: { detail: 'Gone.' } }
    const loaded = await store.load(1)

    expect(loaded).toBeNull()
    expect(store.credit).toBeNull()
    expect(store.balance).toBe(0)
    expect(store.error).toBe('Gone.')
    expect(store.loading).toBe(false)
  })
})

describe('useCreditStore.createTopUp', () => {
  it('returns the id of the new invoice and releases the flag', async () => {
    table[`POST ${TOP_UP_PATH}`] = { body: 88 }
    const store = useCreditStore()

    const invoiceId = await store.createTopUp(40)

    expect(invoiceId).toBe(88)
    expect(JSON.parse(requestsTo(stub.requests, TOP_UP_PATH)[0]?.body ?? '')).toEqual({ amount: 40 })
    expect(store.topUpBusy).toBe(false)
    expect(store.topUpError).toBe('')
  })

  it('returns null and keeps the sentence when the backend refuses the amount', async () => {
    table[`POST ${TOP_UP_PATH}`] = refusal
    const store = useCreditStore()

    const invoiceId = await store.createTopUp(1)

    expect(invoiceId).toBeNull()
    expect(store.topUpError).toBe('Refused.')
    expect(store.topUpBusy).toBe(false)
  })

  it('clears the previous refusal when it tries again', async () => {
    table[`POST ${TOP_UP_PATH}`] = refusal
    const store = useCreditStore()
    await store.createTopUp(1)

    table[`POST ${TOP_UP_PATH}`] = { body: 9 }
    await store.createTopUp(50)

    expect(store.topUpError).toBe('')
  })
})

describe('useCreditStore.applyToInvoice', () => {
  it('applies the credit and reads the balance again from the first page', async () => {
    table['POST /api/portal/client/invoices/7/credit'] = { status: 204 }
    table[`GET ${CREDIT_PATH}`] = { body: credit(20) }
    const store = useCreditStore()

    const applied = await store.applyToInvoice('7', 30)

    expect(applied).toBe(true)
    expect(JSON.parse(requestsTo(stub.requests, '/api/portal/client/invoices/7/credit')[0]?.body ?? '')).toEqual({ amount: 30 })
    expect(creditReads()).toEqual([{ page: '1', pageSize: String(CREDIT_PAGE_SIZE) }])
    expect(store.balance).toBe(20)
    expect(store.applyBusy).toBe(false)
  })

  it('returns false, keeps the sentence and does not read the balance when the backend refuses', async () => {
    table['POST /api/portal/client/invoices/7/credit'] = refusal
    const store = useCreditStore()

    const applied = await store.applyToInvoice(7, 500)

    expect(applied).toBe(false)
    expect(store.applyError).toBe('Refused.')
    expect(store.applyBusy).toBe(false)
    expect(creditReads()).toHaveLength(0)
  })
})
