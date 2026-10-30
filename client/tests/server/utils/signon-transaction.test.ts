/**
 * Tests for `server/utils/signon-transaction.ts`: one sealed cookie per sign-in attempt, found only
 * through its own `state`, single use, bounded in number, expiring, and named with `__Host-` outside
 * development.
 *
 * @module tests/server/utils/signon-transaction.test
 */

import { createHash, randomBytes } from 'node:crypto'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { makeEvent, setRuntimeConfig } from '../../helpers/bffHarness'
import {
  createTransaction,
  deleteTransaction,
  hasPendingInteractiveTransaction,
  openTransaction,
  transactionCookieName
} from '~/server/utils/signon-transaction'
import { SSO_FLOW_COOKIE_MAX_AGE, SSO_MAX_LIVE_TRANSACTIONS } from '~/server/utils/auth-cookie'
import { SignonTransactionKind } from '~/types/signontransactionkind'
import { SignonTransactionStatus } from '~/types/signontransactionstatus'

/** The start of the fake clock. */
const START = new Date('2030-01-01T10:00:00Z')

/** Milliseconds in one sign-in attempt lifetime. */
const LIFETIME_MS = SSO_FLOW_COOKIE_MAX_AGE * 1000

/**
 * Starts a transaction in a fresh response and returns the cookie the browser would keep.
 *
 * @param state - The `state` of the attempt.
 * @param held - The cookies the browser already holds.
 * @param kind - The kind of attempt.
 * @returns The event the start wrote to, and the browser's cookies afterwards.
 */
const start = (state: string, held: Record<string, string> = {}, kind: SignonTransactionKind = SignonTransactionKind.Interactive) => {
  const event = makeEvent({ cookies: held })
  createTransaction(event, { state, verifier: `verifier-${state}`, returnTo: '/client/invoices', kind })
  const cookies = { ...held }
  for (const name of event.deleted) {
    Reflect.deleteProperty(cookies, name)
  }
  for (const [name, written] of Object.entries(event.written)) {
    cookies[name] = written.value
  }
  return { event, cookies }
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(START)
  setRuntimeConfig({ sessionEncryptionKey: '' })
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllEnvs()
})

describe('transactionCookieName', () => {
  it('is __Host- plus sso_tx_ plus the first 16 hex characters of sha256(state) outside development', () => {
    const digest = createHash('sha256').update('abc').digest('hex').slice(0, 16)

    expect(transactionCookieName('abc')).toBe(`__Host-sso_tx_${digest}`)
  })

  it('has no prefix in development, where the site is served over plain HTTP', () => {
    vi.stubEnv('NODE_ENV', 'development')
    const digest = createHash('sha256').update('abc').digest('hex').slice(0, 16)

    expect(transactionCookieName('abc')).toBe(`sso_tx_${digest}`)
  })

  it('differs for different states', () => {
    expect(transactionCookieName('one')).not.toBe(transactionCookieName('two'))
  })
})

describe('createTransaction and openTransaction', () => {
  it('round-trips the attempt through a sealed cookie that holds none of it in the clear', () => {
    const { event, cookies } = start('state-1')

    const name = transactionCookieName('state-1')
    expect(Object.keys(event.written)).toEqual([name])
    expect(event.written[name]?.value).not.toContain('verifier-state-1')
    const lookup = openTransaction(makeEvent({ cookies }), 'state-1')
    expect(lookup.status).toBe(SignonTransactionStatus.Found)
    expect(lookup.transaction).toMatchObject({
      v: 1,
      state: 'state-1',
      verifier: 'verifier-state-1',
      returnTo: '/client/invoices',
      kind: SignonTransactionKind.Interactive,
      createdAt: START.getTime(),
    })
  })

  it('writes the cookie httpOnly, Secure, SameSite=Lax, path /, for the attempt lifetime', () => {
    const { event } = start('state-1')

    expect(event.written[transactionCookieName('state-1')]?.options).toMatchObject({
      httpOnly: true, secure: true, sameSite: 'lax', path: '/', maxAge: SSO_FLOW_COOKIE_MAX_AGE,
    })
  })

  it('writes a plain-named, non-Secure cookie in development', () => {
    vi.stubEnv('NODE_ENV', 'development')

    const { event, cookies } = start('state-1')

    const [name] = Object.keys(event.written)
    expect(name).toMatch(/^sso_tx_[0-9a-f]{16}$/)
    expect(event.written[name ?? '']?.options).toMatchObject({ secure: false, path: '/' })
    expect(openTransaction(makeEvent({ cookies }), 'state-1').status).toBe(SignonTransactionStatus.Found)
  })

  it('remembers a silent attempt and a missing return path', () => {
    const event = makeEvent()
    createTransaction(event, { state: 's', verifier: 'v', returnTo: null, kind: SignonTransactionKind.Silent })
    const cookies = { [transactionCookieName('s')]: event.written[transactionCookieName('s')]?.value ?? '' }

    expect(openTransaction(makeEvent({ cookies }), 's').transaction).toMatchObject({ returnTo: null, kind: SignonTransactionKind.Silent })
  })

  it('finds nothing for a state it never started, or for no state', () => {
    const { cookies } = start('state-1')

    expect(openTransaction(makeEvent({ cookies }), 'state-2').status).toBe(SignonTransactionStatus.Missing)
    expect(openTransaction(makeEvent({ cookies }), undefined).status).toBe(SignonTransactionStatus.Missing)
    expect(openTransaction(makeEvent({ cookies }), '').status).toBe(SignonTransactionStatus.Missing)
  })

  it('rejects a tampered cookie', () => {
    const { cookies } = start('state-1')
    const name = transactionCookieName('state-1')
    const value = cookies[name] ?? ''
    const bytes = Buffer.from(value.slice(3), 'base64url')
    bytes[bytes.length - 1] = (bytes[bytes.length - 1] ?? 0) ^ 1
    cookies[name] = `v1.${bytes.toString('base64url')}`

    const lookup = openTransaction(makeEvent({ cookies }), 'state-1')

    expect(lookup).toEqual({ status: SignonTransactionStatus.Tampered, transaction: null })
  })

  it('rejects a cookie moved to the name of another state, so one attempt cannot answer for another', () => {
    const { cookies } = start('state-1')
    const moved = { [transactionCookieName('state-2')]: cookies[transactionCookieName('state-1')] ?? '' }

    expect(openTransaction(makeEvent({ cookies: moved }), 'state-2').status).toBe(SignonTransactionStatus.Tampered)
  })

  it('rejects plain text and a cookie sealed under another key', () => {
    const { cookies } = start('state-1')
    const name = transactionCookieName('state-1')
    setRuntimeConfig({ sessionEncryptionKey: randomBytes(32).toString('base64') })

    expect(openTransaction(makeEvent({ cookies }), 'state-1').status).toBe(SignonTransactionStatus.Tampered)
    expect(openTransaction(makeEvent({ cookies: { [name]: 'state-1' } }), 'state-1').status).toBe(SignonTransactionStatus.Tampered)
  })

  it('opens what another replica sealed under the same configured key', () => {
    setRuntimeConfig({ sessionEncryptionKey: randomBytes(32).toString('base64') })
    const { cookies } = start('state-1')

    expect(openTransaction(makeEvent({ cookies }), 'state-1').status).toBe(SignonTransactionStatus.Found)
  })

  it('treats an attempt older than its lifetime as expired, and one at the limit as live', () => {
    const { cookies } = start('state-1')

    vi.setSystemTime(START.getTime() + LIFETIME_MS)
    expect(openTransaction(makeEvent({ cookies }), 'state-1').status).toBe(SignonTransactionStatus.Found)

    vi.setSystemTime(START.getTime() + LIFETIME_MS + 1)
    expect(openTransaction(makeEvent({ cookies }), 'state-1')).toEqual({ status: SignonTransactionStatus.Expired, transaction: null })
  })

  it('deletes nothing when it opens', () => {
    const { cookies } = start('state-1')
    const event = makeEvent({ cookies })

    openTransaction(event, 'state-1')
    openTransaction(event, 'other')

    expect(event.deleted).toEqual([])
  })
})

describe('deleteTransaction', () => {
  it('deletes only the cookie of that state', () => {
    const event = makeEvent()

    deleteTransaction(event, 'state-1')

    expect(event.deleted).toEqual([transactionCookieName('state-1')])
  })
})

describe('cap and prune', () => {
  it('keeps every attempt while within the limit, deleting none', () => {
    let cookies: Record<string, string> = {}
    for (let index = 0; index < SSO_MAX_LIVE_TRANSACTIONS; index += 1) {
      vi.setSystemTime(START.getTime() + index * 1000)
      const started = start(`state-${index}`, cookies)
      expect(started.event.deleted).toEqual([])
      cookies = started.cookies
    }

    expect(Object.keys(cookies)).toHaveLength(SSO_MAX_LIVE_TRANSACTIONS)
  })

  it('drops the oldest live attempt when another would exceed the limit', () => {
    let cookies: Record<string, string> = {}
    for (let index = 0; index < SSO_MAX_LIVE_TRANSACTIONS; index += 1) {
      vi.setSystemTime(START.getTime() + index * 1000)
      cookies = start(`state-${index}`, cookies).cookies
    }

    vi.setSystemTime(START.getTime() + 10_000)
    const sixth = start('state-new', cookies)

    expect(sixth.event.deleted).toEqual([transactionCookieName('state-0')])
    expect(Object.keys(sixth.cookies)).toHaveLength(SSO_MAX_LIVE_TRANSACTIONS)
    expect(openTransaction(makeEvent({ cookies: sixth.cookies }), 'state-0').status).toBe(SignonTransactionStatus.Missing)
    expect(openTransaction(makeEvent({ cookies: sixth.cookies }), 'state-1').status).toBe(SignonTransactionStatus.Found)
    expect(openTransaction(makeEvent({ cookies: sixth.cookies }), 'state-new').status).toBe(SignonTransactionStatus.Found)
  })

  it('drops expired and unreadable attempts on create', () => {
    const old = start('state-old')
    vi.setSystemTime(START.getTime() + LIFETIME_MS + 1000)
    const junkName = transactionCookieName('state-junk')
    const held = { ...old.cookies, [junkName]: 'not-a-seal' }

    const fresh = start('state-new', held)

    expect(fresh.event.deleted.sort()).toEqual([junkName, transactionCookieName('state-old')].sort())
    expect(Object.keys(fresh.cookies)).toEqual([transactionCookieName('state-new')])
  })

  it('leaves cookies that are not transaction cookies alone', () => {
    const fresh = start('state-new', { portal_session: 'abc', 'sso_tx_nothex': 'x', sso_silent_tried: '1' })

    expect(fresh.event.deleted).toEqual([])
  })
})

describe('hasPendingInteractiveTransaction', () => {
  it('is true while an interactive attempt is live', () => {
    const { cookies } = start('state-1')

    expect(hasPendingInteractiveTransaction(makeEvent({ cookies }))).toBe(true)
  })

  it('is false for a silent attempt, for an expired one, for a forged one and for none', () => {
    const silent = start('state-s', {}, SignonTransactionKind.Silent)
    const interactive = start('state-i')
    vi.setSystemTime(START.getTime() + LIFETIME_MS + 1)

    expect(hasPendingInteractiveTransaction(makeEvent({ cookies: silent.cookies }))).toBe(false)
    expect(hasPendingInteractiveTransaction(makeEvent({ cookies: interactive.cookies }))).toBe(false)
    expect(hasPendingInteractiveTransaction(makeEvent({ cookies: { [transactionCookieName('x')]: 'forged' } }))).toBe(false)
    expect(hasPendingInteractiveTransaction(makeEvent())).toBe(false)
  })
})
