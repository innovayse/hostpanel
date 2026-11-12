/**
 * Tests for `composables/useCheckoutRenewalSave.ts`: the stored-method session that follows a
 * confirmed checkout payment when the payer asked for it, spent once, and a non-blocking failure
 * when it cannot be opened.
 *
 * The real session starter, auth and client stores, API composables and transport run against a stub
 * server playing the BFF; only the browser's location is a stand-in object, so the redirect to the
 * gateway page is read from it.
 *
 * @module tests/composables/useCheckoutRenewalSave.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCheckoutRenewalSave } from '~/composables/useCheckoutRenewalSave'
import { useAuthStore } from '~/stores/auth'
import { useClientStore } from '~/stores/client'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { SaveMethodMode } from '~/types/savemethodmode'
import { rememberCheckoutSave } from '~/utils/pendingCheckoutSave'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { stubLocalePath } from '~/tests/helpers/runtimeStubs'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** Path of the endpoint that opens a save session. */
const SESSIONS_PATH = '/api/portal/client/stored-payment-methods/sessions'

/** Path of the endpoint that reads the signed-in account. */
const ME_PATH = '/api/portal/client/me'

/** The gateway page the opened session sends the browser to. */
const GATEWAY_PAGE = 'https://gateway.example/save'

/** The account that made the checkout choice. */
const ADA = 'ada@example.com'

/** The stub playing the BFF. */
let stub: StubServer

/** What the BFF answers, by `METHOD /path`. */
let table: StubRouteTable

/** The browser location the session starter writes on a redirect; `failRedirect` makes the write throw. */
let browserLocation: { origin: string, href: string }

/**
 * Makes the BFF open a session that sends the browser to the gateway page.
 */
const openSessions = (): void => {
  table[`POST ${SESSIONS_PATH}`] = { body: { sessionId: 's1', mode: SaveMethodMode.Redirect, redirectUrl: GATEWAY_PAGE } }
}

/**
 * Signs an account into the auth store, as the page would have it after the trip to the bank.
 *
 * @param email - The account's address.
 */
const signIn = (email: string): void => {
  useAuthStore().user = makeClientUser({ email })
}

/**
 * Makes the browser refuse to leave for the gateway page, so the session starter itself throws.
 */
const failRedirect = (): void => {
  Object.defineProperty(browserLocation, 'href', {
    get: () => '',
    set: () => {
      throw new Error('navigation blocked')
    },
  })
}

beforeEach(async () => {
  setActivePinia(createPinia())
  window.sessionStorage.clear()
  stubLocalePath('/en')
  browserLocation = { origin: 'https://panel.test', href: '' }
  vi.stubGlobal('location', browserLocation)
  table = {}
  stub = await startTransport(routeTable(table))
  signIn(ADA)
})

afterEach(async () => {
  await stub.close()
  vi.unstubAllGlobals()
})

describe('useCheckoutRenewalSave', () => {
  it('starts nothing when the payer did not tick the box', async () => {
    openSessions()
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(requestsTo(stub.requests, SESSIONS_PATH)).toHaveLength(0)
    expect(save.saving.value).toBe(false)
    expect(save.failed.value).toBe(false)
  })

  it('starts a session at the module the order was paid through, with the consent given at checkout', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    const [sent] = requestsTo(stub.requests, SESSIONS_PATH)
    expect(JSON.parse(sent?.body ?? '')).toEqual(expect.objectContaining({
      gatewayModuleId: 'card_gateway',
      consentAccepted: true,
      consentTextVersion: ConsentTextVersion.CheckoutAgreement,
    }))
    expect(browserLocation.href).toBe(GATEWAY_PAGE)
    // The browser is on its way to the gateway page: the page keeps showing progress, never a failure.
    expect(save.saving.value).toBe(true)
    expect(save.failed.value).toBe(false)
  })

  it('starts one session only, however often the paid page is opened', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)
    await save.startIfChosen(42)

    expect(requestsTo(stub.requests, SESSIONS_PATH)).toHaveLength(1)
  })

  it('raises the notice, and stops showing progress, when the session cannot be opened', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    table[`POST ${SESSIONS_PATH}`] = { status: 400, body: { type: 'STORED_CREDENTIALS_NOT_SUPPORTED', detail: 'Not supported.' } }
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(save.failed.value).toBe(true)
    expect(save.saving.value).toBe(false)
  })

  it('raises the notice instead of throwing when the start itself throws', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    failRedirect()
    const save = useCheckoutRenewalSave()

    await expect(save.startIfChosen(42)).resolves.toBeUndefined()

    expect(save.failed.value).toBe(true)
    expect(save.saving.value).toBe(false)
  })

  it('drops the choice of a declined payment, so a later confirmation cannot act on it', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    const save = useCheckoutRenewalSave()

    save.discard(42)
    await save.startIfChosen(42)

    expect(requestsTo(stub.requests, SESSIONS_PATH)).toHaveLength(0)
  })

  it('starts nothing, and spends the choice, when another account returns to the page', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    signIn('bob@example.com')
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(requestsTo(stub.requests, SESSIONS_PATH)).toHaveLength(0)
    expect(save.saving.value).toBe(false)
    expect(window.sessionStorage.length).toBe(0)
  })

  it('starts nothing when nobody can be identified', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    useAuthStore().user = null
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(requestsTo(stub.requests, SESSIONS_PATH)).toHaveLength(0)
    expect(window.sessionStorage.length).toBe(0)
  })

  it('still starts the session when the account read fails but the account is already known', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    // The auth store has not got the account yet, the client store has it, and the read that would refresh it fails.
    useAuthStore().user = null
    useClientStore().user = makeClientUser({ email: ADA })
    table[`GET ${ME_PATH}`] = { status: 500, body: { type: 'SERVER_ERROR', detail: 'Offline.' } }
    const save = useCheckoutRenewalSave()

    await expect(save.startIfChosen(42)).resolves.toBeUndefined()

    expect(JSON.parse(requestsTo(stub.requests, SESSIONS_PATH)[0]?.body ?? '')).toEqual(expect.objectContaining({
      gatewayModuleId: 'card_gateway',
      consentTextVersion: ConsentTextVersion.CheckoutAgreement,
    }))
    expect(save.failed.value).toBe(false)
  })

  it('starts nothing, and does not throw, when the account read fails and nobody is known', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    openSessions()
    useAuthStore().user = null
    table[`GET ${ME_PATH}`] = { status: 500, body: { type: 'SERVER_ERROR', detail: 'Offline.' } }
    const save = useCheckoutRenewalSave()

    await expect(save.startIfChosen(42)).resolves.toBeUndefined()

    expect(requestsTo(stub.requests, SESSIONS_PATH)).toHaveLength(0)
    expect(save.failed.value).toBe(false)
    expect(window.sessionStorage.length).toBe(0)
  })

  it('does not load the account when no choice was made', async () => {
    useAuthStore().user = null
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(requestsTo(stub.requests, ME_PATH)).toHaveLength(0)
  })
})
