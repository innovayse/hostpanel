/**
 * Tests for `GET /account`: the widget link "Manage all services" leads to the shell account centre.
 *
 * @module tests/server/routes/account.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, startBackend } from '../../helpers/bffHarness'
import type { RouteModule } from '../../helpers/bffHarness'
import type { StubServer } from '../../helpers/stubServer'

/** The stub the harness needs for its runtime config. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  setRuntimeConfig({ public: { authMode: 'sso', mainUrl: 'https://shell.example.test/' } })
  route = await import('~/server/routes/account.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /account', () => {
  it('redirects to the account centre of the shell', () => {
    const event = makeEvent()

    route.default(event)

    expect(event.redirectedTo).toBe('https://shell.example.test/account')
  })

  it('answers 404 where the widget is not enabled', async () => {
    setRuntimeConfig({ public: { authMode: 'local', mainUrl: 'https://shell.example.test' } })

    expect(await refusal(() => route.default(makeEvent()))).toMatchObject({ statusCode: 404 })
  })
})
