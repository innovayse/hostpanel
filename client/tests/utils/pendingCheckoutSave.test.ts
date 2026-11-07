/**
 * Tests for `utils/pendingCheckoutSave.ts`: the per-order "save for renewals" choice that survives
 * the trip to the bank, is spent the first time it is read and belongs to the account that made it.
 *
 * @module tests/utils/pendingCheckoutSave.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { PENDING_CHECKOUT_SAVE_KEY_PREFIX, PENDING_CHECKOUT_SAVE_TTL_MS } from '~/constants/storedMethodReturn'
import { clearCheckoutSaves, consumeCheckoutSave, forgetCheckoutSave, hasCheckoutSave, rememberCheckoutSave } from '~/utils/pendingCheckoutSave'

/** The account that makes the choices in these tests. */
const ADA = 'ada@example.com'

/** Another account sharing the same browser. */
const BOB = 'bob@example.com'

afterEach(() => {
  vi.useRealTimers()
  window.sessionStorage.clear()
})

describe('pendingCheckoutSave', () => {
  it('reads back the module that was remembered for the order', () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)

    expect(consumeCheckoutSave('42', ADA)).toBe('card_gateway')
  })

  it('spends the choice on the first read, so a reload cannot act on it twice', () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)

    expect(consumeCheckoutSave(42, ADA)).toBe('card_gateway')
    expect(consumeCheckoutSave(42, ADA)).toBeNull()
    expect(window.sessionStorage.getItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`)).toBeNull()
  })

  it('keeps one choice per order', () => {
    rememberCheckoutSave(1, 'gateway_a', ADA)
    rememberCheckoutSave(2, 'gateway_b', ADA)

    expect(consumeCheckoutSave(2, ADA)).toBe('gateway_b')
    expect(consumeCheckoutSave(1, ADA)).toBe('gateway_a')
  })

  it('answers null when nothing was remembered', () => {
    expect(consumeCheckoutSave(42, ADA)).toBeNull()
  })

  it('forgets a choice that was withdrawn', () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)

    forgetCheckoutSave(42)

    expect(consumeCheckoutSave(42, ADA)).toBeNull()
  })

  it('ignores a choice older than its lifetime', () => {
    vi.useFakeTimers()
    rememberCheckoutSave(42, 'card_gateway', ADA)

    vi.advanceTimersByTime(PENDING_CHECKOUT_SAVE_TTL_MS + 1)

    expect(consumeCheckoutSave(42, ADA)).toBeNull()
  })

  it('answers null, and drops the entry, for a damaged one', () => {
    window.sessionStorage.setItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`, '{not json')

    expect(consumeCheckoutSave(42, ADA)).toBeNull()
    expect(window.sessionStorage.getItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`)).toBeNull()
  })

  it('answers null for an entry of the wrong shape', () => {
    window.sessionStorage.setItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`, JSON.stringify({ gatewayModuleId: 7 }))

    expect(consumeCheckoutSave(42, ADA)).toBeNull()
  })

  it('answers null, and drops the entry, for an entry that names no account', () => {
    window.sessionStorage.setItem(
      `${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`,
      JSON.stringify({ gatewayModuleId: 'card_gateway', expiresAt: Date.now() + PENDING_CHECKOUT_SAVE_TTL_MS })
    )

    expect(consumeCheckoutSave(42, ADA)).toBeNull()
    expect(window.sessionStorage.getItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`)).toBeNull()
  })
})

describe('pendingCheckoutSave ownership', () => {
  it('gives another account nothing, and drops the entry so the owner cannot use it either', () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)

    expect(consumeCheckoutSave(42, BOB)).toBeNull()
    expect(window.sessionStorage.getItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`)).toBeNull()
    expect(consumeCheckoutSave(42, ADA)).toBeNull()
  })

  it('gives nothing when the current account is unknown, and drops the entry', () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)

    expect(consumeCheckoutSave(42, null)).toBeNull()
    expect(window.sessionStorage.getItem(`${PENDING_CHECKOUT_SAVE_KEY_PREFIX}42`)).toBeNull()
  })

  it('matches the account regardless of letter case', () => {
    rememberCheckoutSave(42, 'card_gateway', 'Ada@Example.com')

    expect(consumeCheckoutSave(42, 'ADA@example.COM')).toBe('card_gateway')
  })

  it('remembers nothing when the account is unknown', () => {
    rememberCheckoutSave(42, 'card_gateway', null)
    rememberCheckoutSave(43, 'card_gateway', undefined)

    expect(hasCheckoutSave(42)).toBe(false)
    expect(hasCheckoutSave(43)).toBe(false)
  })

  it('reports an entry without spending it', () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)

    expect(hasCheckoutSave(42)).toBe(true)
    expect(hasCheckoutSave(42)).toBe(true)
    expect(hasCheckoutSave(43)).toBe(false)
  })
})

describe('clearCheckoutSaves', () => {
  it('removes every remembered choice of every account and leaves other storage alone', () => {
    rememberCheckoutSave(1, 'gateway_a', ADA)
    rememberCheckoutSave(2, 'gateway_b', BOB)
    window.sessionStorage.setItem('unrelated', 'keep')

    clearCheckoutSaves()

    expect(hasCheckoutSave(1)).toBe(false)
    expect(hasCheckoutSave(2)).toBe(false)
    expect(window.sessionStorage.getItem('unrelated')).toBe('keep')
  })
})
