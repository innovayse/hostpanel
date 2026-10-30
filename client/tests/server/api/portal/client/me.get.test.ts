/**
 * Tests for `GET /api/portal/client/me`: the backend profile passes through as sent, with the
 * permissions added; in single sign-on mode the email and 2FA flag come from the SSO identity.
 *
 * @module tests/server/api/portal/client/me.get.test
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeEvent, refusal, setRuntimeConfig, startBackend, signedIn } from '../../../../helpers/bffHarness'
import type { RouteModule } from '../../../../helpers/bffHarness'
import type { StubReply, StubRequest, StubServer } from '../../../../helpers/stubServer'
import { Permission } from '~/composables/usePermissions'
import { AuthMode } from '~/types/authmode'

/** The stub playing both the SSO profile endpoint and the C# API. */
let backend: StubServer

/** The route under test. */
let route: RouteModule

/** The billing profile the C# API answers with. */
const billing = {
  id: 7,
  firstName: 'Ann',
  lastName: 'Lee',
  companyName: 'Acme',
  email: 'ann@b.test',
  phone: '+1',
  address: {
    street: 'Main 1',
    address2: 'Apt 2',
    city: 'Yerevan',
    state: 'AR',
    postCode: '0001',
    country: 'AM',
  },
  paymentMethod: 'card',
  language: 'hy',
  currency: 'USD',
  notifications: {
    general: true,
    invoice: false,
    support: true,
    product: false,
    domain: true,
    affiliate: false,
    marketingOptIn: false,
    statusUpdate: false,
  },
  twoFactorEnabled: true,
  contacts: [],
}

/** The permissions the C# API reports for the signed-in person: two of the thirteen flags. */
const granted = Permission.ViewDomains | Permission.ViewEmails

/**
 * Answers the SSO profile, the account (permissions) and the billing profile by path.
 *
 * @param request - The request the stub received.
 * @returns The reply for that path.
 */
const answer = (request: StubRequest): StubReply => {
  if (request.url === '/api/account/profile') {
    return { body: { id: 'sso-1', firstName: 'Id', lastName: 'Entity', email: 'sso@b.test', phoneNumber: '+9', totpEnabled: true } }
  }
  if (request.url === '/api/auth/me') {
    return { body: { email: 'sso@b.test', roles: ['Client'], emailVerified: true, clientPermissions: granted } }
  }
  return { body: billing }
}

beforeEach(async () => {
  backend = await startBackend()
  route = await import('~/server/api/portal/client/me.get')
})

afterEach(async () => {
  await backend.close()
})

describe('GET /api/portal/client/me in single sign-on mode', () => {
  beforeEach(() => {
    setRuntimeConfig({ authMode: AuthMode.Sso })
    backend.respondWith(answer)
  })

  it('passes the billing profile through with the identity email and 2FA flag, and forwards the language to the billing call', async () => {
    const result = await route.default(makeEvent({ cookies: await signedIn('jwt'), headers: { 'x-language': 'ru' } }))

    expect(backend.requests[0]?.headers['authorization']).toBe('Bearer jwt')
    expect(backend.requests[1]?.url).toBe('/api/clients/me')
    expect(backend.requests[1]?.headers['x-language']).toBe('ru')
    expect(result).toEqual({
      ...billing,
      email: 'sso@b.test',
      twoFactorEnabled: true,
      permissions: granted,
      actsForAccount: true,
      hasClientProfile: true,
    })
  })

  it('takes the permissions from the API instead of granting everything', async () => {
    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

    expect(backend.requests.map(request => request.url)).toContain('/api/auth/me')
    expect(result['permissions']).toBe(granted)
  })

  it('grants nothing when the account cannot be read', async () => {
    backend.respondWith(request => (request.url === '/api/auth/me' ? { status: 500, body: {} } : answer(request)))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

    expect(result['permissions']).toBe(Permission.None)
  })

  describe('actsForAccount', () => {
    /**
     * Answers the SSO profile and the billing profile as usual, and `/auth/me` with the given account fields.
     *
     * @param account - What the account answer adds to the permissions: `clientId`, `externalAccount`, or neither.
     */
    const respondWithAccount = (account: Record<string, unknown>): void => {
      backend.respondWith(request => (
        request.url === '/api/auth/me'
          ? { body: { email: 'sso@b.test', roles: ['Client'], emailVerified: true, clientPermissions: granted, ...account } }
          : answer(request)
      ))
    }

    it('is true for the owner of a client record', async () => {
      respondWithAccount({ clientId: 7, externalAccount: null })

      const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

      expect(result['actsForAccount']).toBe(true)
    })

    it('is true for a member an identity plugin vouches for, who has no client record yet', async () => {
      respondWithAccount({ clientId: null, externalAccount: { provider: 'acme-sso', id: 'u-1' } })

      const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

      expect(result['actsForAccount']).toBe(true)
    })

    it('is false for a signed-in person who acts for no account, such as a first-time buyer', async () => {
      respondWithAccount({ clientId: null, externalAccount: null, clientPermissions: 0 })

      const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

      expect(result['actsForAccount']).toBe(false)
      expect(result['permissions']).toBe(Permission.None)
    })

    it('is true when the account cannot be read, so nothing that needs a permission is offered on a guess', async () => {
      backend.respondWith(request => (request.url === '/api/auth/me' ? { status: 500, body: {} } : answer(request)))

      const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

      expect(result['actsForAccount']).toBe(true)
    })

    it('is true when an older backend sends neither field', async () => {
      respondWithAccount({})

      const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

      expect(result['actsForAccount']).toBe(true)
    })
  })

  it('answers the identity with every billing field null when the billing profile is refused', async () => {
    backend.respondWith(request => (request.url === '/api/clients/me' ? { status: 404, body: { type: 'CLIENT_PROFILE_NOT_FOUND', detail: 'None.' } } : answer(request)))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

    expect(result).toEqual({
      id: null,
      email: 'sso@b.test',
      firstName: 'Id',
      lastName: 'Entity',
      companyName: null,
      phone: '+9',
      address: null,
      language: null,
      currency: null,
      paymentMethod: null,
      notifications: null,
      twoFactorEnabled: true,
      contacts: [],
      permissions: granted,
      actsForAccount: true,
      hasClientProfile: false,
    })
  })

  it('flags a customer account as having a client profile', async () => {
    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

    expect(result['hasClientProfile']).toBe(true)
  })

  it.each([403, 404])('flags the identity as not a customer when the billing profile answers %i', async (status) => {
    backend.respondWith(request => (request.url === '/api/account/profile' ? answer(request) : { status, body: { type: 'FORBIDDEN', detail: 'No.' } }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

    expect(result['hasClientProfile']).toBe(false)
  })

  it('does not flag the identity when the billing profile fails for another reason', async () => {
    backend.respondWith(request => (request.url === '/api/account/profile' ? answer(request) : { status: 500, body: {} }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') })) as Record<string, unknown>

    expect(result['hasClientProfile']).toBe(true)
  })

  it('refuses with 401 without calling anything when there is no session', async () => {
    const error = await refusal(() => route.default(makeEvent()))

    expect(error).toMatchObject({ statusCode: 401 })
    expect(backend.requests).toHaveLength(0)
  })

  it('refuses with the SSO status when the profile call fails', async () => {
    backend.respondWith(() => ({ status: 403, body: {} }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 403, data: { code: 'SIGN_ON_REFUSED' } })
  })

  it('answers 502 with a code and no address when the SSO fails or cannot be reached', async () => {
    backend.respondWith(() => ({ status: 500, body: {} }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 502, data: { code: 'SIGN_ON_UNAVAILABLE' } })
    expect(String(error['message'] ?? '')).not.toContain(backend.baseUrl)
  })
})

describe('GET /api/portal/client/me in local mode', () => {
  beforeEach(() => {
    setRuntimeConfig({ authMode: AuthMode.Local })
  })

  it('maps the billing profile to the portal shape', async () => {
    backend.respondWith(request => (request.url === '/api/auth/me' ? answer(request) : { body: billing }))

    const result = await route.default(makeEvent({ cookies: await signedIn('jwt') }))

    expect(backend.requests.map(request => request.url)).toEqual(['/api/clients/me', '/api/auth/me'])
    expect(result).toEqual({ ...billing, permissions: granted, actsForAccount: true, hasClientProfile: true })
  })

  it('passes the backend refusal through with its code and sentence', async () => {
    backend.respondWith(() => ({ status: 404, body: { type: 'CLIENT_PROFILE_NOT_FOUND', detail: 'No profile.' } }))

    const error = await refusal(async () => route.default(makeEvent({ cookies: await signedIn('jwt') })))

    expect(error).toMatchObject({ statusCode: 404, statusMessage: 'No profile.', data: { code: 'CLIENT_PROFILE_NOT_FOUND' } })
  })
})
