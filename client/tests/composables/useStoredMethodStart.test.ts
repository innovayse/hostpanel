/**
 * Tests for `composables/useStoredMethodStart.ts`: the shared start of a stored-method save
 * session, used by the saved-methods card and by the post-checkout save.
 *
 * Nothing of our own is replaced: the real store, API composable, transport and address check run
 * against a stub server playing the BFF; only the browser's location is a stand-in object, so a
 * handed-on redirect is read from it.
 *
 * @module tests/composables/useStoredMethodStart.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStoredMethodStart } from '~/composables/useStoredMethodStart'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { SaveMethodMode } from '~/types/savemethodmode'
import { readPendingStoredSession } from '~/utils/pendingStoredSession'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { StubServer } from '~/tests/helpers/stubServer'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'

/** Path of the endpoint that opens a save session. */
const SESSIONS_PATH = '/api/portal/client/stored-payment-methods/sessions'

/** The stub playing the BFF. */
let stub: StubServer

/** The browser location the composable reads and, on a redirect, writes. */
let browserLocation: { origin: string, href: string }

/** What the BFF answers, by `METHOD /path`; a test sets the session reply. */
let table: StubRouteTable

beforeEach(async () => {
  setActivePinia(createPinia())
  window.sessionStorage.clear()
  stubLocalePath('/en')
  browserLocation = { origin: 'https://panel.test', href: '' }
  vi.stubGlobal('location', browserLocation)
  table = {}
  stub = await startTransport(routeTable(table))
})

afterEach(async () => {
  await stub.close()
  vi.unstubAllGlobals()
})

describe('useStoredMethodStart', () => {
  it('opens a session with the account payment tab as the return address and follows the redirect', async () => {
    table[`POST ${SESSIONS_PATH}`] = { body: { sessionId: 's1', mode: SaveMethodMode.Redirect, redirectUrl: 'https://gateway.example/save' } }
    const { start } = useStoredMethodStart()

    const handedOn = await start('card_gateway', true, ConsentTextVersion.AccountCard)

    expect(handedOn).toBe(true)
    const [sent] = requestsTo(stub.requests, SESSIONS_PATH)
    const body = JSON.parse(sent?.body ?? '') as { gatewayModuleId: string, consentAccepted: boolean, consentTextVersion: string, returnUrl: string }
    expect(body.gatewayModuleId).toBe('card_gateway')
    expect(body.consentAccepted).toBe(true)
    expect(body.consentTextVersion).toBe(ConsentTextVersion.AccountCard)
    expect(new URL(body.returnUrl).pathname).toBe('/en/client/account')
    expect(new URL(body.returnUrl).search).toBe('?tab=payment&storedReturn=1')
    expect(browserLocation.href).toBe('https://gateway.example/save')
    expect(readPendingStoredSession()).toEqual({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
  })

  it('does not redirect, and says so, when the session is refused', async () => {
    table[`POST ${SESSIONS_PATH}`] = { status: 400, body: { type: 'STORED_CREDENTIALS_NOT_SUPPORTED', detail: 'Not supported.' } }
    const { start } = useStoredMethodStart()

    expect(await start('card_gateway', true, ConsentTextVersion.CheckoutAgreement)).toBe(false)

    expect(browserLocation.href).toBe('')
    expect(readPendingStoredSession()).toBeNull()
  })

  it('does not redirect for a session whose address is not safe', async () => {
    table[`POST ${SESSIONS_PATH}`] = { body: { sessionId: 's1', mode: SaveMethodMode.Redirect, redirectUrl: 'javascript:alert(1)' } }
    const { start } = useStoredMethodStart()

    expect(await start('card_gateway', true, ConsentTextVersion.AccountCard)).toBe(false)

    expect(browserLocation.href).toBe('')
  })
})
