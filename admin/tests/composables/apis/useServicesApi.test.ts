/**
 * Tests for the services API composable: paths, verbs, query strings and bodies through the
 * real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useServicesApi } from '@/composables/apis/useServicesApi'
import { BillingCycle, PanelSyncField, PanelSyncOutcome, ServiceStatus, type ServiceBillingTermsPayload, type ServiceHostingAccountPayload } from '@/types/service'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A valid billing section body. */
const billing: ServiceBillingTermsPayload = {
  productId: 2,
  quantity: 1,
  billingCycle: BillingCycle.Monthly,
  firstPaymentAmount: 12,
  recurringAmount: 12,
  nextRenewalAt: null,
  paymentMethod: null,
  promotionCode: null,
  subscriptionId: null,
}

/** A valid hosting account section body. */
const hosting: ServiceHostingAccountPayload = {
  serverId: 3,
  domain: 'example.com',
  provisioningRef: 'acct1',
  dedicatedIp: null,
  username: 'user1',
  password: null,
}

describe('useServicesApi', () => {
  it('listServices sends the client only when given', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const api = useServicesApi()

    await api.listServices({ page: 1, pageSize: 20 })
    await api.listServices({ clientId: 7, page: 2, pageSize: 100 })
    expect(callOf(mock, 0).url).toBe('/api/services?page=1&pageSize=20')
    expect(callOf(mock, 1).url).toBe('/api/services?clientId=7&page=2&pageSize=100')
  })

  it('listServices sends the search, the status, the product and the billing cycle when given', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useServicesApi().listServices({
      page: 1, pageSize: 20, search: 'shop', status: ServiceStatus.Active, productId: 3, billingCycle: BillingCycle.Biennial,
    })

    expect(callOf(mock).url).toBe('/api/services?page=1&pageSize=20&search=shop&status=Active&productId=3&billingCycle=Biennial')
  })

  it('getService reads the detail', async () => {
    const mock = stubFetch(json({ id: 4 }))

    expect(await useServicesApi().getService(4)).toEqual({ id: 4 })
    expect(callOf(mock).url).toBe('/api/services/4')
  })

  it('each section PUTs its own fields to its own endpoint', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useServicesApi()

    await api.updateBillingTerms(4, billing)
    await api.updateHostingAccount(4, hosting)
    await api.updateAutomation(4, { autoRenew: true, overrideAutoSuspend: true, suspendUntil: '2031-01-01', autoTerminateEndOfCycle: false, autoTerminateReason: null })
    await api.updateNotes(4, { adminNotes: 'note' })
    await api.changeStatus(4, { status: ServiceStatus.Suspended })
    await api.correctTerminationDate(4, { terminatedAt: '2030-06-01' })

    expect(callOf(mock, 0)).toMatchObject({ url: '/api/services/4/billing', method: 'PUT', body: billing })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/services/4/hosting-account', method: 'PUT', body: hosting })
    expect(callOf(mock, 2)).toMatchObject({
      url: '/api/services/4/automation',
      method: 'PUT',
      body: { autoRenew: true, overrideAutoSuspend: true, suspendUntil: '2031-01-01', autoTerminateEndOfCycle: false, autoTerminateReason: null },
    })
    expect(callOf(mock, 3)).toMatchObject({ url: '/api/services/4/notes', method: 'PUT', body: { adminNotes: 'note' } })
    expect(callOf(mock, 4)).toMatchObject({ url: '/api/services/4/status', method: 'PUT', body: { status: 'Suspended' } })
    expect(callOf(mock, 5)).toMatchObject({ url: '/api/services/4/termination-date', method: 'PUT', body: { terminatedAt: '2030-06-01' } })
  })

  it('getPanelLoginUrl reads the single sign-on url', async () => {
    const mock = stubFetch(json({ url: 'https://panel.example/login' }))

    expect(await useServicesApi().getPanelLoginUrl(4)).toEqual({ url: 'https://panel.example/login' })
    expect(callOf(mock).url).toBe('/api/provisioning/4/panel-sso')
  })

  it('getPanelLoginDetails reads how to sign in by hand', async () => {
    const details = { username: 'ada', domain: 'site.example', serverAddress: '203.0.113.9', loginUrl: 'https://panel.example:8083/login/' }
    const mock = stubFetch(json(details))

    expect(await useServicesApi().getPanelLoginDetails(4)).toEqual(details)
    expect(callOf(mock).url).toBe('/api/provisioning/4/panel-login')
  })

  it('syncServiceWithPanel POSTs the password choice to the admin route and returns what was done', async () => {
    const answer = { fields: [{ field: PanelSyncField.Status, outcome: PanelSyncOutcome.Applied, panelValue: 'Suspended' }] }
    const mock = stubFetch(json(answer))

    expect(await useServicesApi().syncServiceWithPanel(4, { pushPassword: true })).toEqual(answer)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/services/4/sync-panel', method: 'POST', body: { pushPassword: true } })
  })

  it('listPackages reads the package names of the service server', async () => {
    const mock = stubFetch(json({ names: ['Starter', 'Business'] }))

    expect(await useServicesApi().listPackages(4)).toEqual({ names: ['Starter', 'Business'] })
    expect(callOf(mock)).toMatchObject({ url: '/api/provisioning/4/packages' })
  })

  it('provisioning commands POST to /provisioning with their bodies', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useServicesApi()

    await api.provisionService(4)
    await api.changePassword(4, 'secret')
    await api.changePackage(4, 'Gold')
    await api.suspendService(4, { reason: 'Abuse' })
    await api.unsuspendService(4)
    await api.terminateService(4, { reason: 'Requested' })
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/provisioning/4/provision', method: 'POST' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/provisioning/4/change-password', body: { newPassword: 'secret' } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/provisioning/4/change-package', body: { newPackage: 'Gold' } })
    expect(callOf(mock, 3)).toMatchObject({ url: '/api/provisioning/4/suspend', body: { reason: 'Abuse' } })
    expect(callOf(mock, 4)).toMatchObject({ url: '/api/provisioning/4/unsuspend', method: 'POST' })
    expect(callOf(mock, 5)).toMatchObject({ url: '/api/provisioning/4/terminate', body: { reason: 'Requested' } })
  })
})
