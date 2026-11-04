/**
 * Tests for `GET /api/portal/client/plugins/slots/:slot/widgets/:plugin/:widget`: it forwards the
 * checked ids and context to the backend's widget view and passes the view through, passes the
 * backend's not found on unchanged (a widget not listed for this caller, a foreign service); malformed
 * ids or context are a coded 400 and never reach the backend.
 *
 * @module tests/server/api/portal/client/plugins/slots/[slot]/widgets/[plugin]/[widget].get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, startBackend, signedIn } from '../../../../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../../../../helpers/stubServer'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/plugins/slots/[slot]/widgets/[plugin]/[widget].get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/plugins/slots/:slot/widgets/:plugin/:widget', () => {
  it('forwards the ids and the service and passes the view through', async () => {
    /** The backend's widget view. */
    const view = { pluginId: 'acme', id: 'tab', title: 'Site', view: { schema: 1, blocks: [] } }
    backend.respondWith(() => ({ body: view }))

    const result = await route.default(makeEvent({
      cookies: await signedIn('jwt'),
      params: { slot: 'client.service.tabs', plugin: 'acme', widget: 'tab' },
      query: { serviceId: '4' },
    }))

    expect(backend.requests[0]?.url).toBe('/api/client/plugins/slots/client.service.tabs/widgets/acme/tab?serviceId=4')
    expect(result).toEqual(view)
  })

  it('forwards a widget of a slot with no context without a query', async () => {
    backend.respondWith(() => ({ body: { pluginId: 'acme', id: 'card', title: null, view: { schema: 1, blocks: [] } } }))

    await route.default(makeEvent({ cookies: await signedIn('jwt'), params: { slot: 'client.dashboard.cards', plugin: 'acme', widget: 'card' } }))

    expect(backend.requests[0]?.url).toBe('/api/client/plugins/slots/client.dashboard.cards/widgets/acme/card')
  })

  it('passes on the backend\'s not found for a widget it would not list for this caller', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND', title: 'Not Found', status: 404, detail: 'This widget is not available.' } }))

    const error = await refusal(async () => route.default(makeEvent({
      cookies: await signedIn('jwt'),
      params: { slot: 'client.dashboard.cards', plugin: 'acme', widget: 'hidden' },
    })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND' } })
  })

  it('passes on the backend\'s not found for a service that is not the reader\'s', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'SERVICE_NOT_FOUND', title: 'Not Found', status: 404, detail: 'The service was not found.' } }))

    const error = await refusal(async () => route.default(makeEvent({
      cookies: await signedIn('jwt'),
      params: { slot: 'client.service.tabs', plugin: 'acme', widget: 'tab' },
      query: { serviceId: '9' },
    })))

    expect(error).toMatchObject({ statusCode: 404, data: { code: 'SERVICE_NOT_FOUND' } })
    expect(backend.requests[0]?.url).toBe('/api/client/plugins/slots/client.service.tabs/widgets/acme/tab?serviceId=9')
  })

  it.each([
    ['a widget id that is not an identifier', { slot: 'client.dashboard.cards', plugin: 'acme', widget: 'a/b' }, {}],
    ['a plugin id that is not an identifier', { slot: 'client.dashboard.cards', plugin: '..', widget: 'card' }, {}],
    ['a slot id that is not a dotted name', { slot: '../admin', plugin: 'acme', widget: 'card' }, {}],
    ['a service id that is not a positive integer', { slot: 'client.service.tabs', plugin: 'acme', widget: 'tab' }, { serviceId: 'x' }],
    ['a service id past 32 bits', { slot: 'client.service.tabs', plugin: 'acme', widget: 'tab' }, { serviceId: '2147483648' }],
    ['a plugin id with capitals', { slot: 'client.dashboard.cards', plugin: 'Acme', widget: 'card' }, {}],
    ['a plugin id with an underscore', { slot: 'client.dashboard.cards', plugin: 'acme_sites', widget: 'card' }, {}],
    ['a widget id starting with an underscore', { slot: 'client.dashboard.cards', plugin: 'acme', widget: '_tab' }, {}],
    ['a widget id with capitals', { slot: 'client.dashboard.cards', plugin: 'acme', widget: 'Tab' }, {}],
  ])('refuses %s with a coded 400 and asks the backend nothing', async (_name, params, query) => {
    await expect((async () => route.default(makeEvent({ cookies: await signedIn('jwt'), params, query })))()).rejects.toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })
})
