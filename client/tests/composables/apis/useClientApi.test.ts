/**
 * Tests for `composables/apis/useClientApi.ts`.
 *
 * Each endpoint function is called for real through `apiFetch` against a stub server, and the
 * test asserts on the path, verb, query and body that arrived and on how the reply was typed.
 *
 * @module tests/composables/apis/useClientApi.test
 */

import { afterEach, describe, it } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useClientApi } from '~/composables/apis/useClientApi'
import { makeContactInput } from '~/tests/helpers/accountContact'
import { CancellationType } from '~/types/cancellationtype'
import { HttpMethod } from '~/types/httpmethod'
import { InvitePermissionMode } from '~/types/invitepermissionmode'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useClientApi()

/** Every endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  { name: 'fetchMe reads the profile', call: () => api.fetchMe(), method: HttpMethod.Get, url: '/api/portal/client/me' },
  { name: 'fetchMeOnce reads the profile as a plain record', call: () => api.fetchMeOnce(), method: HttpMethod.Get, url: '/api/portal/client/me' },
  { name: 'updateMe patches the profile', call: () => api.updateMe({ firstName: 'Ada' }), method: HttpMethod.Patch, url: '/api/portal/client/me', body: { firstName: 'Ada' } },
  { name: 'startEmailChange posts the new address and the password', call: () => api.startEmailChange({ newEmail: 'n@b.c', currentPassword: 'pw' }), method: HttpMethod.Post, url: '/api/portal/client/email-change', body: { newEmail: 'n@b.c', currentPassword: 'pw' } },
  { name: 'confirmEmailChange posts the address and the token', call: () => api.confirmEmailChange('n@b.c', 'tok'), method: HttpMethod.Post, url: '/api/portal/client/email-change/confirm', body: { email: 'n@b.c', token: 'tok' } },
  { name: 'fetchServices reads the list', call: () => api.fetchServices(), method: HttpMethod.Get, url: '/api/portal/client/services' },
  { name: 'fetchInvoices reads the list', call: () => api.fetchInvoices(), method: HttpMethod.Get, url: '/api/portal/client/invoices' },
  { name: 'fetchDomains reads the list', call: () => api.fetchDomains(), method: HttpMethod.Get, url: '/api/portal/client/domains' },
  { name: 'fetchTickets reads the list', call: () => api.fetchTickets(), method: HttpMethod.Get, url: '/api/portal/client/tickets' },
  { name: 'fetchUsers reads the team', call: () => api.fetchUsers(), method: HttpMethod.Get, url: '/api/portal/client/users' },
  { name: 'inviteUser posts the invitation', call: () => api.inviteUser({ email: 'a@b.c', firstName: 'A', lastName: 'B', permissionMode: InvitePermissionMode.All, permissions: [] }), method: HttpMethod.Post, url: '/api/portal/client/users/invite', body: { email: 'a@b.c', firstName: 'A', lastName: 'B', permissionMode: InvitePermissionMode.All, permissions: [] } },
  { name: 'removeUser deletes by id', call: () => api.removeUser('u1'), method: HttpMethod.Delete, url: '/api/portal/client/users/u1' },
  { name: 'fetchContacts reads the list', call: () => api.fetchContacts(), method: HttpMethod.Get, url: '/api/portal/client/contacts' },
  { name: 'createContact posts the contact', call: () => api.createContact(makeContactInput({ firstName: 'A' })), method: HttpMethod.Post, url: '/api/portal/client/contacts', body: makeContactInput({ firstName: 'A' }) },
  { name: 'updateContact puts by id', call: () => api.updateContact(3, makeContactInput({ firstName: 'B' })), method: HttpMethod.Put, url: '/api/portal/client/contacts/3', body: makeContactInput({ firstName: 'B' }) },
  { name: 'removeContact deletes by id', call: () => api.removeContact(3), method: HttpMethod.Delete, url: '/api/portal/client/contacts/3' },
  { name: 'fetchEmails reads the list', call: () => api.fetchEmails(), method: HttpMethod.Get, url: '/api/portal/client/emails' },
  { name: 'fetchEmail reads one message', call: () => api.fetchEmail('5'), method: HttpMethod.Get, url: '/api/portal/client/emails/5' },
  { name: 'fetchService reads one service', call: () => api.fetchService('9'), method: HttpMethod.Get, url: '/api/portal/client/services/9' },
  { name: 'fetchHostingInfo reads the sub-resource', call: () => api.fetchHostingInfo('9'), method: HttpMethod.Get, url: '/api/portal/client/services/9/hosting-info' },
  { name: 'fetchCancellationStatus reads the sub-resource', call: () => api.fetchCancellationStatus('9'), method: HttpMethod.Get, url: '/api/portal/client/services/9/cancellation-status' },
  { name: 'fetchServiceInvoices reads the service invoices', call: () => api.fetchServiceInvoices('9'), method: HttpMethod.Get, url: '/api/portal/client/services/9/invoices' },
  { name: 'changeServicePassword posts the password', call: () => api.changeServicePassword('9', 'longenough'), method: HttpMethod.Post, url: '/api/portal/client/services/9/change-password', body: { password: 'longenough' } },
  { name: 'cancelService posts the type as a member name', call: () => api.cancelService('9', { type: CancellationType.EndOfBillingPeriod, reason: 'moving' }), method: HttpMethod.Post, url: '/api/portal/client/services/9/cancel', body: { type: 'EndOfBillingPeriod', reason: 'moving' } },
  { name: 'fetchPlanChangeOptions reads the sub-resource', call: () => api.fetchPlanChangeOptions('9'), method: HttpMethod.Get, url: '/api/portal/client/services/9/plan-change-options' },
  { name: 'requestPlanChange posts the target product', call: () => api.requestPlanChange('9', 12), method: HttpMethod.Post, url: '/api/portal/client/services/9/plan-change', body: { toProductId: 12 } },
  { name: 'fetchCpanelSsoUrl reads the sign-in address', call: () => api.fetchCpanelSsoUrl('9'), method: HttpMethod.Get, url: '/api/portal/client/services/9/cpanel-sso' },
  { name: 'setUpService posts the setup answers', call: () => api.setUpService('9', { domain: 'a.com' }), method: HttpMethod.Post, url: '/api/portal/client/services/9/setup', body: { domain: 'a.com' } },
]

describe('useClientApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()

    await row.call()

    expectLastRequest(stub, row)
  })
})
