/**
 * Tests for `GET /api/portal/public/config`: the header widget learns where the sign-on service is
 * from the portal it was served by.
 *
 * @module tests/server/routes/api/portal/public/config.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, startBackend } from '../../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../../helpers/bffHarness'
import type { StubServer } from '../../../../../helpers/stubServer'

/** The stub the harness needs for its runtime config. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

beforeEach(async () => {
  backend = await startBackend()
  setRuntimeConfig({ public: { authMode: 'sso', mainUrl: 'https://shell.example.test', ssoPublicUrl: 'https://sso.example.test' } })
  route = await import('~/server/routes/api/portal/public/config.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/public/config', () => {
  it('answers the sign-on address the browser reaches', () => {
    expect(route.default(makeEvent())).toEqual({ ssoUrl: 'https://sso.example.test' })
  })

  it('answers 503 with a code when the sign-on address is not configured', async () => {
    setRuntimeConfig({ public: { authMode: 'sso', mainUrl: 'https://shell.example.test', ssoPublicUrl: '' } })

    expect(await refusal(() => route.default(makeEvent()))).toMatchObject({ statusCode: 503, data: { code: 'SSO_NOT_CONFIGURED' } })
  })

  it('answers 404 where the widget is not enabled', async () => {
    setRuntimeConfig({ public: { authMode: 'local', mainUrl: 'https://shell.example.test', ssoPublicUrl: 'https://sso.example.test' } })

    expect(await refusal(() => route.default(makeEvent()))).toMatchObject({ statusCode: 404 })
  })
})
