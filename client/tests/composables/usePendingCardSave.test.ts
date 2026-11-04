/**
 * Tests for `usePendingCardSave`: the session a payer leaves for a gateway with survives the trip
 * in `sessionStorage` only, expires, and is dropped on request.
 *
 * @module tests/composables/usePendingCardSave.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PENDING_CARD_SAVE_TTL_MS, usePendingCardSave } from '~/composables/usePendingCardSave'

describe('usePendingCardSave', () => {
  beforeEach(() => {
    sessionStorage.clear()
    localStorage.clear()
  })

  afterEach(() => {
    sessionStorage.clear()
    localStorage.clear()
    vi.useRealTimers()
  })

  it('recalls nothing for a module that stored no session', () => {
    expect(usePendingCardSave().recall('inecobank')).toBeUndefined()
  })

  it('stores a session per module in session storage only and recalls it', () => {
    const { remember, recall } = usePendingCardSave()

    remember('inecobank', 'sess-1')

    expect(sessionStorage.getItem('pending-card-save:inecobank')).toContain('sess-1')
    expect(localStorage.getItem('pending-card-save:inecobank')).toBeNull()
    expect(recall('inecobank')).toBe('sess-1')
    expect(recall('other')).toBeUndefined()
  })

  it('stops recalling a session after it expires and drops the entry', () => {
    vi.useFakeTimers()
    const { remember, recall } = usePendingCardSave()
    remember('inecobank', 'sess-1')

    vi.advanceTimersByTime(PENDING_CARD_SAVE_TTL_MS + 1)

    expect(recall('inecobank')).toBeUndefined()
    expect(sessionStorage.getItem('pending-card-save:inecobank')).toBeNull()
  })

  it('drops a malformed entry', () => {
    sessionStorage.setItem('pending-card-save:inecobank', '{not json')

    expect(usePendingCardSave().recall('inecobank')).toBeUndefined()
    expect(sessionStorage.getItem('pending-card-save:inecobank')).toBeNull()
  })

  it('forgets a session on request', () => {
    const { remember, recall, forget } = usePendingCardSave()
    remember('inecobank', 'sess-1')

    forget('inecobank')

    expect(recall('inecobank')).toBeUndefined()
  })
})
