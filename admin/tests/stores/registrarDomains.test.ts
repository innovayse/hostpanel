/**
 * Tests for the registrar domains store: a page loads and pages by cursor (Next follows the cursor the page carries, Previous
 * goes back to one already seen), a refused load keeps the page on screen and puts the API sentence in `error`, and an import
 * marks the row as in the panel, or puts the sentence and the field messages of a refusal in `actionError` and `fieldErrors`.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useRegistrarDomainsStore } from '@/stores/registrarDomains'
import { json, problem, stubFetch, callOf, invalid } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

/** One listed domain. */
const listed = (name: string, inPanel = false) => ({ name, expiresAt: '2027-01-01T00:00:00Z', inPanel })

describe('registrarDomainsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchFirst loads the first page and knows whether another follows', async () => {
    const mock = stubFetch(json({ domains: [listed('one.com')], nextCursor: 'c-2' }))
    const store = useRegistrarDomainsStore()

    expect(await store.fetchFirst('registrar-one')).toBe(true)

    expect(callOf(mock).url).toBe('/api/domains/registrar/registrar-one')
    expect(store.domains).toEqual([listed('one.com')])
    expect(store.hasNext).toBe(true)
    expect(store.hasPrevious).toBe(false)
    expect(store.pageNumber).toBe(1)
    expect(store.loading).toBe(false)
  })

  it('fetchNext follows the cursor of the page on screen and fetchPrevious goes back to the cursor that opened it', async () => {
    const mock = stubRoutes({
      'GET /api/domains/registrar/registrar-one?cursor=c-2': () => json({ domains: [listed('two.com')], nextCursor: null }),
      'GET /api/domains/registrar/registrar-one': () => json({ domains: [listed('one.com')], nextCursor: 'c-2' }),
    })
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')

    expect(await store.fetchNext('registrar-one')).toBe(true)
    expect(store.domains).toEqual([listed('two.com')])
    expect(store.pageNumber).toBe(2)
    expect(store.hasNext).toBe(false)
    expect(store.hasPrevious).toBe(true)

    expect(await store.fetchPrevious('registrar-one')).toBe(true)
    expect(store.domains).toEqual([listed('one.com')])
    expect(store.pageNumber).toBe(1)
    expect(mock).toHaveBeenCalledTimes(3)
  })

  it('there is nothing to fetch past the last page or before the first', async () => {
    const mock = stubFetch(json({ domains: [], nextCursor: null }))
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')

    expect(await store.fetchNext('registrar-one')).toBe(false)
    expect(await store.fetchPrevious('registrar-one')).toBe(false)
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('a refused next page keeps the page on screen and reports the sentence', async () => {
    stubRoutes({
      'GET /api/domains/registrar/registrar-one?cursor=c-2': () => problem('The registrar refused the saved credentials.', 503),
      'GET /api/domains/registrar/registrar-one': () => json({ domains: [listed('one.com')], nextCursor: 'c-2' }),
    })
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')

    expect(await store.fetchNext('registrar-one')).toBe(false)

    expect(store.error).toBe('The registrar refused the saved credentials.')
    expect(store.domains).toEqual([listed('one.com')])
    expect(store.pageNumber).toBe(1)
  })

  it('fetchFirst starts again from the first page', async () => {
    stubRoutes({
      'GET /api/domains/registrar/registrar-one?cursor=c-2': () => json({ domains: [listed('two.com')], nextCursor: null }),
      'GET /api/domains/registrar/registrar-one': () => json({ domains: [listed('one.com')], nextCursor: 'c-2' }),
    })
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')
    await store.fetchNext('registrar-one')

    await store.fetchFirst('registrar-one')

    expect(store.pageNumber).toBe(1)
    expect(store.domains).toEqual([listed('one.com')])
  })

  it('importDomain posts the client and the name and marks the listed domain as in the panel', async () => {
    const mock = stubRoutes({
      'POST /api/domains/registrar/registrar-one/import': () => new Response(null, { status: 201 }),
      'GET /api/domains/registrar/registrar-one': () => json({ domains: [listed('one.com'), listed('two.com')], nextCursor: null }),
    })
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')

    expect(await store.importDomain('registrar-one', { clientId: 7, domainName: 'one.com' })).toBe(true)

    expect(store.domains).toEqual([listed('one.com', true), listed('two.com')])
    expect(callOf(mock, 1)).toMatchObject({ method: 'POST', body: { clientId: 7, domainName: 'one.com' } })
  })

  it('a refused import puts the sentence in actionError and the field messages in fieldErrors, and marks nothing', async () => {
    stubRoutes({
      'POST /api/domains/registrar/registrar-one/import': () =>
        invalid('Validation failed.', { clientId: ['Client ID must be a positive number.'] }),
      'GET /api/domains/registrar/registrar-one': () => json({ domains: [listed('one.com')], nextCursor: null }),
    })
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')

    expect(await store.importDomain('registrar-one', { clientId: 0, domainName: 'one.com' })).toBe(false)

    expect(store.actionError).toBe('Validation failed.')
    expect(store.fieldErrors.clientId).toBe('Client ID must be a positive number.')
    expect(store.domains).toEqual([listed('one.com')])
  })

  it('reset forgets the pages and the sentences', async () => {
    stubFetch(json({ domains: [listed('one.com')], nextCursor: 'c-2' }))
    const store = useRegistrarDomainsStore()
    await store.fetchFirst('registrar-one')

    store.reset()

    expect(store.domains).toEqual([])
    expect(store.hasNext).toBe(false)
    expect(store.pageNumber).toBe(1)
    expect(store.error).toBeNull()
    expect(store.actionError).toBeNull()
  })
})
