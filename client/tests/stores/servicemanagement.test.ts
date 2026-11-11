/**
 * Tests for `stores/servicemanagement.ts`.
 *
 * The store, not the service pages, owns the in-flight flags and refusal sentences, so these
 * assertions pin the contract the pages render from.
 *
 * @module tests/stores/servicemanagement.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { CancellationType } from '~/types/cancellationtype'

/** The mocked client-area API composable, re-created for every test. */
const api = {
  changeServicePassword: vi.fn(),
  cancelService: vi.fn(),
  fetchServiceInvoices: vi.fn(),
  fetchCpanelSsoUrl: vi.fn(),
  setUpService: vi.fn(),
  fetchService: vi.fn(),
  fetchHostingInfo: vi.fn(),
  fetchCancellationStatus: vi.fn()
}

/** The client store stand-in, so setup can be seen invalidating the service list. */
const clientStore = { servicesLoaded: true }

vi.stubGlobal('ref', ref)

vi.mock('~/composables/apis/useClientApi', () => ({
  /**
   * Stands in for the real API composable so the store runs without transport.
   *
   * @returns The mocked endpoints.
   */
  useClientApi: () => api
}))

vi.mock('~/stores/client', () => ({
  /**
   * Stands in for the client store, which setup only marks stale.
   *
   * @returns The shared stand-in.
   */
  useClientStore: () => clientStore
}))

const { useServiceManagementStore } = await import('~/stores/servicemanagement')
const { ActionOutcome } = await import('~/types/actionoutcome')

describe('useServiceManagementStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(api).forEach(fn => fn.mockReset())
    clientStore.servicesLoaded = true
  })

  it('changePassword reports Succeeded and sets the success flag', async () => {
    api.changeServicePassword.mockResolvedValue({})
    const store = useServiceManagementStore()

    const outcome = await store.changePassword('3', 'longenough')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(api.changeServicePassword).toHaveBeenCalledWith('3', 'longenough')
    expect(store.passwordSuccess).toBe(true)
    expect(store.passwordChanging).toBe(false)
  })

  it('changePassword reports Failed and keeps the sentence', async () => {
    api.changeServicePassword.mockRejectedValue(new Error('no'))
    const store = useServiceManagementStore()

    const outcome = await store.changePassword('3', 'longenough')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.passwordSuccess).toBe(false)
    expect(store.passwordError).not.toBe('')
    expect(store.passwordChanging).toBe(false)
  })

  it('cancelService sends type and reason and marks the request done', async () => {
    api.cancelService.mockResolvedValue({})
    const store = useServiceManagementStore()

    const outcome = await store.cancelService('3', CancellationType.Immediate, 'moving')

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(api.cancelService).toHaveBeenCalledWith('3', { type: 'Immediate', reason: 'moving' })
    expect(store.cancelDone).toBe(true)
  })

  it('cancelService reports Failed and keeps the sentence', async () => {
    api.cancelService.mockRejectedValue(new Error('no'))
    const store = useServiceManagementStore()

    const outcome = await store.cancelService('3', CancellationType.EndOfBillingPeriod, '')

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.cancelDone).toBe(false)
    expect(store.cancelError).not.toBe('')
    expect(store.cancelSubmitting).toBe(false)
  })

  it('loadInvoices stores the list once and does not refetch', async () => {
    api.fetchServiceInvoices.mockResolvedValue({ invoices: [{ id: 1 }], unattributedInvoiceCount: 2 })
    const store = useServiceManagementStore()

    await store.loadInvoices('3')
    await store.loadInvoices('3')

    expect(api.fetchServiceInvoices).toHaveBeenCalledTimes(1)
    expect(store.relatedInvoices).toHaveLength(1)
    expect(store.unattributedInvoices).toBe(2)
    expect(store.invoicesLoaded).toBe(true)
    expect(store.invoicesLoading).toBe(false)
  })

  it('loadInvoices keeps the failure instead of an empty list claim', async () => {
    api.fetchServiceInvoices.mockRejectedValue(new Error('no'))
    const store = useServiceManagementStore()

    await store.loadInvoices('3')

    expect(store.invoicesError).toBeTruthy()
    expect(store.invoicesLoaded).toBe(false)
    expect(store.relatedInvoices).toEqual([])
  })

  it('fetchCpanelUrl returns the address on success', async () => {
    api.fetchCpanelSsoUrl.mockResolvedValue({ url: 'https://panel.example/sso' })
    const store = useServiceManagementStore()

    const url = await store.fetchCpanelUrl('3')

    expect(url).toBe('https://panel.example/sso')
    expect(store.ssoError).toBe('')
    expect(store.ssoLoading).toBe(false)
  })

  it('fetchCpanelUrl treats an empty address as a failure', async () => {
    api.fetchCpanelSsoUrl.mockResolvedValue({ url: '' })
    const store = useServiceManagementStore()

    const url = await store.fetchCpanelUrl('3')

    expect(url).toBeNull()
    expect(store.ssoError).not.toBe('')
  })

  it('openControlPanel opens the single-sign-on address', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    api.fetchCpanelSsoUrl.mockResolvedValue({ url: 'https://panel.example/sso' })
    const store = useServiceManagementStore()

    await store.openControlPanel('3', 'srv.test')

    expect(open).toHaveBeenCalledWith('https://panel.example/sso', '_blank', 'noopener')
    open.mockRestore()
  })

  it('openControlPanel falls back to the plain panel login on the server host', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    api.fetchCpanelSsoUrl.mockRejectedValue(new Error('no'))
    const store = useServiceManagementStore()

    await store.openControlPanel('3', 'srv.test')

    expect(open).toHaveBeenCalledWith('https://srv.test:2083', '_blank', 'noopener')
    expect(store.ssoError).not.toBe('')
    open.mockRestore()
  })

  it('openControlPanel opens nothing when sign-in fails and the host is unknown', async () => {
    const open = vi.spyOn(window, 'open').mockImplementation(() => null)
    api.fetchCpanelSsoUrl.mockRejectedValue(new Error('no'))
    const store = useServiceManagementStore()

    await store.openControlPanel('3', '')

    expect(open).not.toHaveBeenCalled()
    open.mockRestore()
  })

  it('setUpService marks the service list stale on success', async () => {
    api.setUpService.mockResolvedValue({})
    const store = useServiceManagementStore()

    const outcome = await store.setUpService('3', { domain: 'example.com' })

    expect(outcome).toBe(ActionOutcome.Succeeded)
    expect(clientStore.servicesLoaded).toBe(false)
    expect(store.setupSubmitting).toBe(false)
  })

  it('setUpService reports Failed, keeps the sentence and leaves the list cached', async () => {
    api.setUpService.mockRejectedValue(new Error('no'))
    const store = useServiceManagementStore()

    const outcome = await store.setUpService('3', {})

    expect(outcome).toBe(ActionOutcome.Failed)
    expect(store.setupError).not.toBe('')
    expect(clientStore.servicesLoaded).toBe(true)
  })

  it('reset clears the state left by a previous service', async () => {
    api.changeServicePassword.mockResolvedValue({})
    const store = useServiceManagementStore()
    await store.changePassword('3', 'longenough')

    store.reset()

    expect(store.passwordSuccess).toBe(false)
    expect(store.invoicesLoading).toBe(true)
    expect(store.invoicesLoaded).toBe(false)
  })
})

describe('useServiceManagementStore reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    Object.values(api).forEach(fn => fn.mockReset())
  })

  it('keeps the service, and the API sentence with no service when the read is refused', async () => {
    api.fetchService.mockResolvedValueOnce({ id: 3, name: 'Pro' }).mockRejectedValueOnce({ data: { detail: 'Not yours.' } })
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
    api.fetchService.mockResolvedValueOnce({ id: 3, name: 'Pro' }).mockResolvedValueOnce({ id: 3, name: 'Max' })
    const store = useServiceManagementStore()
    await store.loadService('3')

    const reading = store.loadService('3', true)
    expect(store.service).toMatchObject({ name: 'Pro' })
    await reading

    expect(store.service).toMatchObject({ name: 'Max' })
  })

  it('marks a read refused as not found, and clears the mark on the next read', async () => {
    api.fetchService.mockRejectedValueOnce({ data: { data: { code: PortalErrorCode.ServiceNotFound } } }).mockResolvedValueOnce({ id: 3, name: 'Pro' })
    const store = useServiceManagementStore()

    await store.loadService('9')
    expect(store.serviceNotFound).toBe(true)

    await store.loadService('3')
    expect(store.serviceNotFound).toBe(false)
  })

  it('keeps the hosting info and falls back to the empty default when it cannot be read', async () => {
    api.fetchHostingInfo.mockResolvedValueOnce({ nameservers: ['ns1.a'], ssl: null }).mockRejectedValueOnce(new Error('down'))
    const store = useServiceManagementStore()

    await store.loadHostingInfo('3')
    expect(store.hostingInfo.nameservers).toEqual(['ns1.a'])

    await store.loadHostingInfo('3')
    expect(store.hostingInfo).toEqual({ nameservers: [], ssl: null })
  })

  it('keeps the cancellation status and falls back to not pending when it cannot be read', async () => {
    api.fetchCancellationStatus.mockResolvedValueOnce({ pending: true, type: CancellationType.Immediate }).mockRejectedValueOnce(new Error('down'))
    const store = useServiceManagementStore()

    await store.loadCancellationStatus('3')
    expect(store.cancelStatus).toMatchObject({ pending: true, type: CancellationType.Immediate })

    await store.loadCancellationStatus('3')
    expect(store.cancelStatus).toEqual({ pending: false })
  })

  it('clears the previous service when a service page opens', async () => {
    api.fetchService.mockResolvedValue({ id: 3 })
    const store = useServiceManagementStore()
    await store.loadService('3')

    store.reset()

    expect(store.service).toBeNull()
    expect(store.serviceError).toBe('')
    expect(store.cancelStatus).toEqual({ pending: false })
  })
})
