/**
 * Tests for `stores/servicemanagement.ts`, through the real client API composable, the real client store and the
 * transport against a stub server.
 *
 * The store, not the service pages, owns the in-flight flags and refusal sentences, so these
 * assertions pin the contract the pages render from. Nothing of our own is replaced: the stub plays the BFF, and
 * only the browser's `window.open` is observed.
 *
 * @module tests/stores/servicemanagement.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { heldReply, requestsTo, routeTable } from '../helpers/stubRoutes'
import type { StubRouteTable } from '../helpers/stubRoutes'
import type { StubServer } from '../helpers/stubServer'
import { bodyOf } from '../helpers/requestBody'
import { startTransport } from '../helpers/stubTransport'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { useClientStore } from '~/stores/client'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { ActionOutcome } from '~/types/actionoutcome'
import { CancellationType } from '~/types/cancellationtype'
import { HttpMethod } from '~/types/httpmethod'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies the stub gives, by `METHOD /path`; each test fills the ones it needs. */
let table: StubRouteTable

/** The base path of service 3. */
const SERVICE_3 = '/api/portal/client/services/3'

/** The reply of a request the BFF refuses without a sentence, the way an unreachable backend ends. */
const REFUSED = { status: 403 }

beforeEach(async () => {
  setActivePinia(createPinia())
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  vi.restoreAllMocks()
  await stub.close()
})

describe('useServiceManagementStore', () => {
  it('changePassword reports Succeeded and sets the success flag', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/change-password`] = { body: {} }
    const store = useServiceManagementStore()

    const outcome = await store.changePassword('3', 'longenough')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(requestsTo(stub.requests, `${SERVICE_3}/change-password`)).toHaveLength(1)
    expect(bodyOf(stub.requests[0])).toEqual({ password: 'longenough' })
    expect(store.passwordSuccess).toBe(true)
    expect(store.passwordChanging).toBe(false)
  })

  it('changePassword reports Failed and keeps the sentence', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/change-password`] = REFUSED
    const store = useServiceManagementStore()

    const outcome = await store.changePassword('3', 'longenough')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.passwordSuccess).toBe(false)
    expect(store.passwordError).not.toBe('')
    expect(store.passwordChanging).toBe(false)
  })

  it('cancelService sends type and reason and marks the request done', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/cancel`] = { body: {} }
    const store = useServiceManagementStore()

    const outcome = await store.cancelService('3', CancellationType.Immediate, 'moving')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(requestsTo(stub.requests, `${SERVICE_3}/cancel`)).toHaveLength(1)
    expect(bodyOf(stub.requests[0])).toEqual({ type: CancellationType.Immediate, reason: 'moving' })
    expect(store.cancelDone).toBe(true)
  })

  it('cancelService reports Failed and keeps the sentence', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/cancel`] = REFUSED
    const store = useServiceManagementStore()

    const outcome = await store.cancelService('3', CancellationType.EndOfBillingPeriod, '')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.cancelDone).toBe(false)
    expect(store.cancelError).not.toBe('')
    expect(store.cancelSubmitting).toBe(false)
  })

  it('loadInvoices stores the list once and does not refetch', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/invoices`] = { body: { invoices: [{ id: 1 }], unattributedInvoiceCount: 2 } }
    const store = useServiceManagementStore()

    await store.loadInvoices('3')
    await store.loadInvoices('3')

    expect(requestsTo(stub.requests, `${SERVICE_3}/invoices`)).toHaveLength(1)
    expect(store.relatedInvoices).toHaveLength(1)
    expect(store.unattributedInvoices).toBe(2)
    expect(store.invoicesLoaded).toBe(true)
    expect(store.invoicesLoading).toBe(false)
  })

  it('loadInvoices keeps the failure instead of an empty list claim', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/invoices`] = REFUSED
    const store = useServiceManagementStore()

    await store.loadInvoices('3')

    expect(store.invoicesError).toBeTruthy()
    expect(store.invoicesLoaded).toBe(false)
    expect(store.relatedInvoices).toEqual([])
  })

  it('fetchCpanelUrl returns the address on success', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/cpanel-sso`] = { body: { url: 'https://panel.example/sso' } }
    const store = useServiceManagementStore()

    const url = await store.fetchCpanelUrl('3')

    expect(url).toBe('https://panel.example/sso')
    expect(store.ssoError).toBe('')
    expect(store.ssoLoading).toBe(false)
  })

  it('fetchCpanelUrl treats an empty address as a failure', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/cpanel-sso`] = { body: { url: '' } }
    const store = useServiceManagementStore()

    const url = await store.fetchCpanelUrl('3')

    expect(url).toBeNull()
    expect(store.ssoError).not.toBe('')
  })

  it('openControlPanel opens the single-sign-on address', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    table[`${HttpMethod.Get} ${SERVICE_3}/cpanel-sso`] = { body: { url: 'https://panel.example/sso' } }
    const store = useServiceManagementStore()

    await store.openControlPanel('3', 'srv.test')

    expect(open).toHaveBeenCalledWith('https://panel.example/sso', '_blank', 'noopener')
  })

  it('openControlPanel falls back to the plain panel login on the server host', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    table[`${HttpMethod.Get} ${SERVICE_3}/cpanel-sso`] = REFUSED
    const store = useServiceManagementStore()

    await store.openControlPanel('3', 'srv.test')

    expect(open).toHaveBeenCalledWith('https://srv.test:2083', '_blank', 'noopener')
    expect(store.ssoError).not.toBe('')
  })

  it('openControlPanel opens nothing when sign-in fails and the host is unknown', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    table[`${HttpMethod.Get} ${SERVICE_3}/cpanel-sso`] = REFUSED
    const store = useServiceManagementStore()

    await store.openControlPanel('3', '')

    expect(open).not.toHaveBeenCalled()
  })

  it('setUpService marks the service list stale on success', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/setup`] = { body: {} }
    const clientStore = useClientStore()
    clientStore.servicesLoaded = true
    const store = useServiceManagementStore()

    const outcome = await store.setUpService('3', { domain: 'example.com' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(bodyOf(stub.requests[0])).toEqual({ domain: 'example.com' })
    expect(clientStore.servicesLoaded).toBe(false)
    expect(store.setupSubmitting).toBe(false)
  })

  it('setUpService reports Failed, keeps the sentence and leaves the list cached', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/setup`] = REFUSED
    const clientStore = useClientStore()
    clientStore.servicesLoaded = true
    const store = useServiceManagementStore()

    const outcome = await store.setUpService('3', {})

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.setupError).not.toBe('')
    expect(clientStore.servicesLoaded).toBe(true)
  })

  it('reset clears the state left by a previous service', async () => {
    table[`${HttpMethod.Post} ${SERVICE_3}/change-password`] = { body: {} }
    const store = useServiceManagementStore()
    await store.changePassword('3', 'longenough')

    store.reset()

    expect(store.passwordSuccess).toBe(false)
    expect(store.invoicesLoading).toBe(true)
    expect(store.invoicesLoaded).toBe(false)
  })
})

describe('useServiceManagementStore reads', () => {
  it('keeps the service, and the API sentence with no service when the read is refused', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}`] = { body: { id: 3, name: 'Pro' } }
    table[`${HttpMethod.Get} /api/portal/client/services/4`] = { status: 404, body: { detail: 'Not yours.' } }
    const store = useServiceManagementStore()

    expect(await store.loadService('3')).toMatchObject({ name: 'Pro' })
    expect(store.service).toMatchObject({ id: 3 })

    expect(await store.loadService('4')).toBeNull()
    expect(store.service).toBeNull()
    expect(store.serviceError).toBe('Not yours.')
    expect(store.serviceNotFound).toBe(false)
    expect(store.serviceLoading).toBe(false)
  })

  it('keeps the service on screen while it is read again when asked to', async () => {
    const held = heldReply()
    let reads = 0
    /**
     * Answers the first read at once and holds every later one until the test releases it.
     *
     * @returns The reply.
     */
    table[`${HttpMethod.Get} ${SERVICE_3}`] = () => {
      reads += 1
      return reads === 1 ? { body: { id: 3, name: 'Pro' } } : held.reply()
    }
    const store = useServiceManagementStore()
    await store.loadService('3')

    const reading = store.loadService('3', true)
    expect(store.service).toMatchObject({ name: 'Pro' })
    await vi.waitFor(() => {
      expect(held.waiting()).toBe(1)
    })
    held.release({ body: { id: 3, name: 'Max' } })
    await reading

    expect(store.service).toMatchObject({ name: 'Max' })
  })

  it('marks a read refused as not found, and clears the mark on the next read', async () => {
    table[`${HttpMethod.Get} /api/portal/client/services/9`] = { status: 404, body: { data: { code: PortalErrorCode.ServiceNotFound } } }
    table[`${HttpMethod.Get} ${SERVICE_3}`] = { body: { id: 3, name: 'Pro' } }
    const store = useServiceManagementStore()

    await store.loadService('9')
    expect(store.serviceNotFound).toBe(true)

    await store.loadService('3')
    expect(store.serviceNotFound).toBe(false)
  })

  it('keeps the hosting info and falls back to the empty default when it cannot be read', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/hosting-info`] = { body: { nameservers: ['ns1.a'], ssl: null } }
    const store = useServiceManagementStore()

    await store.loadHostingInfo('3')
    expect(store.hostingInfo.nameservers).toEqual(['ns1.a'])

    table[`${HttpMethod.Get} ${SERVICE_3}/hosting-info`] = REFUSED
    await store.loadHostingInfo('3')
    expect(store.hostingInfo).toEqual({ nameservers: [], ssl: null })
  })

  it('keeps the cancellation status and falls back to not pending when it cannot be read', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/cancellation-status`] = { body: { pending: true, type: CancellationType.Immediate } }
    const store = useServiceManagementStore()

    await store.loadCancellationStatus('3')
    expect(store.cancelStatus).toMatchObject({ pending: true, type: CancellationType.Immediate })

    table[`${HttpMethod.Get} ${SERVICE_3}/cancellation-status`] = REFUSED
    await store.loadCancellationStatus('3')
    expect(store.cancelStatus).toEqual({ pending: false })
  })

  it('clears the previous service when a service page opens', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}`] = { body: { id: 3 } }
    const store = useServiceManagementStore()
    await store.loadService('3')

    store.reset()

    expect(store.service).toBeNull()
    expect(store.serviceError).toBe('')
    expect(store.cancelStatus).toEqual({ pending: false })
  })
})
