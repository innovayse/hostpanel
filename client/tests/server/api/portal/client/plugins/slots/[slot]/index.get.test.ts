/**
 * Tests for `GET /api/portal/client/plugins/slots/:slot`: it forwards a well-formed slot id and
 * service id to the backend's slot index and passes the index through, passes the backend's not found
 * on unchanged (a service that is not the reader's, an unknown slot); a malformed one is a coded 400
 * and never reaches the backend.
 *
 * @module tests/server/api/portal/client/plugins/slots/[slot]/index.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/plugins/slots/[slot]/index.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/plugins/slots/:slot', () => {
  it('forwards the slot and the service and passes the index through', async () => {
    /** The backend's index. */
    const index = { slotId: 'client.service.tabs', widgets: [{ pluginId: 'acme', id: 'tab', view: 'views/tab.json', title: 'Site', order: 0, contextKeys: ['serviceId'] }] }
    backend.respondWith(() => ({ body: index }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt'), params: { slot: 'client.service.tabs' }, query: { serviceId: '4' } }))

    expect(backend.requests[0]?.url).toBe('/api/client/plugins/slots/client.service.tabs?serviceId=4')
    expect(result).toEqual(index)
  })

  it('forwards a slot with no context without a query', async () => {
    backend.respondWith(() => ({ body: { slotId: 'client.dashboard.cards', widgets: [] } }))

    await route.default(makeEvent({ cookies: await signedIn('jwt'), params: { slot: 'client.dashboard.cards' } }))

    expect(backend.requests[0]?.url).toBe('/api/client/plugins/slots/client.dashboard.cards')
  })

  it('passes on the backend\'s not found for a service that is not the reader\'s', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'SERVICE_NOT_FOUND', title: 'Not Found', status: 404, detail: 'The service was not found.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt'), params: { slot: 'client.service.tabs' }, query: { serviceId: '9' } })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'SERVICE_NOT_FOUND' } })
    expect(backend.requests[0]?.url).toBe('/api/client/plugins/slots/client.service.tabs?serviceId=9')
  })

  it('passes on the backend\'s not found for an unknown or admin slot', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'PLUGIN_SLOT_NOT_FOUND', title: 'Not Found', status: 404, detail: 'No such slot.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt'), params: { slot: 'admin.client.tabs' } })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'PLUGIN_SLOT_NOT_FOUND' } })
  })

  it.each([
    ['a slot id that is not a dotted name', { slot: '../admin' }, {}],
    ['a slot id with an empty segment', { slot: 'client..cards' }, {}],
    ['a slot id with capitals', { slot: 'Client.Dashboard' }, {}],
    ['a single-word slot id', { slot: 'client' }, {}],
    ['a service id that is not a positive integer', { slot: 'client.service.tabs' }, { serviceId: '0x4' }],
    ['a zero service id', { slot: 'client.service.tabs' }, { serviceId: '0' }],
    ['a service id past 32 bits', { slot: 'client.service.tabs' }, { serviceId: '2147483648' }],
    ['a service id with a leading zero', { slot: 'client.service.tabs' }, { serviceId: '04' }],
    ['a service id with a second statement', { slot: 'client.service.tabs' }, { serviceId: '1;drop' }],
  ])('refuses %s with a coded 400 and asks the backend nothing', async (_name, params, query) => {
    await expect((async () => route.default(makeEvent({ cookies: await signedIn('jwt'), params, query })))()).rejects.toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })
})
