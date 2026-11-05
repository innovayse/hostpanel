/**
 * Tests for `/api/portal/client/plugins/:plugin/routes/**`: a plugin's JSON route is called with the
 * method, path, query and body of the request (the backend matches and authorises it), a path that
 * could leave the plugin's prefix or a method no route may declare never reaches the backend, and
 * the backend's refusal passes on unchanged.
 *
 * @module tests/server/api/portal/client/plugins/[plugin]/routes/[...path].test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, signedIn, startBackend } from '../../../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../../../helpers/stubServer'
import { HttpMethod } from '~/types/httpmethod'

/** The stub playing the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/plugins/[plugin]/routes/[...path]')
})

afterEach(async () => {
  await backend.close()
})

describe('plugin JSON route', () => {
  it('reads a GET route with the query, the session and the language, and no body', async () => {
    backend.respondWith(() => ({ body: [{ id: 1 }] }))

    const result = await route.default(makeEvent({
      params: { plugin: 'acme-sites', path: 'sites/7/history' },
      query: { page: '2' },
      cookies: await signedIn('jwt'),
      headers: { 'x-language': 'hy' },
    }))

    expect(result).toEqual([{ id: 1 }])
    expect(backend.requests[0]).toMatchObject({ method: 'GET', url: '/api/client/plugins/acme-sites/sites/7/history?page=2', body: '' })
    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(backend.requests[0]?.headers['x-language']).toBe('hy')
  })

  it.each([HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch])('forwards %s with its JSON body', async (method: HttpMethod) => {
    backend.respondWith(() => ({ body: { ok: true } }))

    const result = await route.default(makeEvent({
      method,
      params: { plugin: 'acme-sites', path: 'sites/7' },
      body: { name: 'Blog' },
      cookies: await signedIn('jwt'),
    }))

    expect(result).toEqual({ ok: true })
    expect(backend.requests[0]?.method).toBe(method)
    expect(backend.requests[0]?.url).toBe('/api/client/plugins/acme-sites/sites/7')
    expect(JSON.parse(backend.requests[0]?.body ?? '')).toEqual({ name: 'Blog' })
  })

  it('forwards a DELETE without a body', async () => {
    backend.respondWith(() => ({ status: 204 }))

    await route.default(makeEvent({ method: HttpMethod.Delete, params: { plugin: 'acme-sites', path: 'sites/7' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]).toMatchObject({ method: 'DELETE', url: '/api/client/plugins/acme-sites/sites/7', body: '' })
  })

  it('answers JSON null for a route that answered no content', async () => {
    backend.respondWith(() => ({ status: 204 }))

    const result = await route.default(makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/publish' },
      body: { now: true },
      cookies: await signedIn('jwt'),
    }))

    expect(result).toBeNull()
  })

  it('keeps an encoded slash inside its own segment, so it is one route value', async () => {
    backend.respondWith(() => ({ body: {} }))

    await route.default(makeEvent({ params: { plugin: 'acme-sites', path: 'sites/a%2Fb' }, cookies: await signedIn('jwt') }))

    expect(backend.requests[0]?.url).toBe('/api/client/plugins/acme-sites/sites/a%2Fb')
  })

  it('passes the plugin\'s refusal on with its own code and sentence', async () => {
    backend.respondWith(() => ({ status: 409, body: { type: 'plugin.acme-sites.already_published', detail: 'The site is already published.' } }))

    const error = await refusal(async () => route.default(makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites/7/publish' },
      body: {},
      cookies: await signedIn('jwt'),
    })))

    expect(error).toMatchObject({ statusCode: 409, statusMessage: 'The site is already published.', data: { code: 'plugin.acme-sites.already_published' } })
  })

  it('passes the backend\'s 404 for an undeclared route on unchanged', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'PLUGIN_ROUTE_NOT_FOUND', detail: 'No such route.' } }))

    const error = await refusal(async () => route.default(makeEvent({
      method: HttpMethod.Get,
      params: { plugin: 'acme-sites', path: 'nowhere' },
      cookies: await signedIn('jwt'),
    })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No such route.', data: { code: 'PLUGIN_ROUTE_NOT_FOUND' } })
  })

  it('passes the backend\'s field errors on for the form that sent them', async () => {
    backend.respondWith(() => ({ status: 400, body: { type: 'VALIDATION_ERROR', detail: 'Check the form.', errors: { name: ['Too long.'] } } }))

    const error = await refusal(async () => route.default(makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites' },
      body: { name: 'x'.repeat(500) },
      cookies: await signedIn('jwt'),
    })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'VALIDATION_ERROR', errors: { name: ['Too long.'] } } })
  })

  it.each([
    ['../../admin/clients', 'a climb out of the plugin'],
    ['sites/../../ui/other/pages/x', 'a climb into the host\'s own endpoints'],
    ['%2e%2e/%2e%2e/admin', 'an encoded climb'],
    ['sites/%252e%252e', 'a double encoding'],
    ['sites/a%5Cb', 'a backslash'],
    ['sites//7', 'an empty segment'],
  ])('refuses the path %j (%s) with a coded 400 before the backend', async (path: string, _reason: string) => {
    const error = await refusal(async () => route.default(makeEvent({ params: { plugin: 'acme-sites', path }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_ROUTE_PARAM' } })
    expect(backend.requests).toHaveLength(0)
  })

  it('refuses a malformed plugin id before the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({ params: { plugin: 'ui/../admin', path: 'x' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 400 })
    expect(backend.requests).toHaveLength(0)
  })

  it.each([HttpMethod.Head, HttpMethod.Options])('refuses %s, which no plugin route may declare, with a 405', async (method: HttpMethod) => {
    const error = await refusal(async () => route.default(makeEvent({ method, params: { plugin: 'acme-sites', path: 'x' }, cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 405 })
    expect(backend.requests).toHaveLength(0)
  })

  it('refuses a body that is not a JSON object before the backend', async () => {
    const error = await refusal(async () => route.default(makeEvent({
      method: HttpMethod.Post,
      params: { plugin: 'acme-sites', path: 'sites' },
      body: 'name=x',
      cookies: await signedIn('jwt'),
    })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: 'BAD_BODY' } })
    expect(backend.requests).toHaveLength(0)
  })
})
