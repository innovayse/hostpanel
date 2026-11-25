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
import { waitUntil } from '~/tests/helpers/waitUntil'

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

  it('fetchPanelUrl returns the address on success', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { body: { url: 'https://panel.example/sso' } }
    const store = useServiceManagementStore()

    const url = await store.fetchPanelUrl('3')

    expect(url).toBe('https://panel.example/sso')
    expect(store.ssoError).toBe('')
    expect(store.ssoLoading).toBe(false)
  })

  it('fetchPanelUrl treats an empty address as a failure', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { body: { url: '' } }
    const store = useServiceManagementStore()

    const url = await store.fetchPanelUrl('3')

    expect(url).toBeNull()
    expect(store.ssoError).not.toBe('')
  })

  it('openPanel opens the single-sign-on address', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { body: { url: 'https://panel.example/sso' } }
    const store = useServiceManagementStore()

    await store.openPanel('3')

    expect(open).toHaveBeenCalledWith('https://panel.example/sso', '_blank', 'noopener')
  })

  it('openPanel reads the login details when the serving plugin cannot sign anybody in, and opens nothing', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    const details = { username: 'ada', domain: 'a.test', serverAddress: '203.0.113.9', loginUrl: 'https://panel.example:8083/login/' }
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { status: 409, body: { type: 'PANEL_OPERATION_NOT_SUPPORTED', detail: 'The panel does not offer single sign on.' } }
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-login`] = { body: details }
    const store = useServiceManagementStore()

    await store.openPanel('3')

    expect(open).not.toHaveBeenCalled()
    expect(store.panelLogin).toEqual(details)
    expect(store.ssoError).toBe('')
    expect(store.ssoLoading).toBe(false)
  })

  it('openPanel keeps the sign-on sentence when the panel offers neither an address nor login details', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { status: 409, body: { type: 'PANEL_OPERATION_NOT_SUPPORTED', detail: 'The panel does not offer single sign on.' } }
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-login`] = { status: 409, body: { type: 'PANEL_OPERATION_NOT_SUPPORTED', detail: 'No login details.' } }
    const store = useServiceManagementStore()

    await store.openPanel('3')

    expect(store.panelLogin).toBeNull()
    expect(store.ssoError).toBe('The panel does not offer single sign on.')
  })

  it('openPanel does not ask for login details after any other refusal', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { status: 503, body: { type: 'PANEL_PLUGIN_UNAVAILABLE', detail: 'The panel plugin is switched off.' } }
    const store = useServiceManagementStore()

    await store.openPanel('3')

    expect(requestsTo(stub.requests, `${SERVICE_3}/panel-login`)).toHaveLength(0)
    expect(store.ssoError).toBe('The panel plugin is switched off.')
  })

  it('closePanelLogin forgets the details', async () => {
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { status: 409, body: { type: 'PANEL_OPERATION_NOT_SUPPORTED', detail: 'x' } }
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-login`] = { body: { username: 'ada', domain: '', serverAddress: '203.0.113.9', loginUrl: 'https://panel.example/login' } }
    const store = useServiceManagementStore()
    await store.openPanel('3')

    store.closePanelLogin()

    expect(store.panelLogin).toBeNull()
  })

  it('openPanel opens nothing and keeps the API sentence when the sign-in is refused', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    table[`${HttpMethod.Get} ${SERVICE_3}/panel-sso`] = { status: 409, body: { type: 'PANEL_PLUGIN_UNAVAILABLE', detail: 'The panel plugin is switched off.' } }
    const store = useServiceManagementStore()

    await store.openPanel('3')

    expect(open).not.toHaveBeenCalled()
    expect(store.ssoError).toBe('The panel plugin is switched off.')
  })

  it('panelIconUrl is the portal route of the plugin icon', () => {
    expect(useServiceManagementStore().panelIconUrl('acme-panel')).toBe('/api/portal/client/plugins/acme-panel/icon')
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
    await waitUntil(() => {
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
