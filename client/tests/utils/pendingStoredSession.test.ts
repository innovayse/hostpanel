/**
 * Tests for `utils/pendingStoredSession.ts`.
 *
 * @module tests/utils/pendingStoredSession.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { PENDING_STORED_SESSION_KEY } from '~/constants/storedMethodReturn'
import { clearPendingStoredSession, readPendingStoredSession, writePendingStoredSession } from '~/utils/pendingStoredSession'

afterEach(() => {
  window.sessionStorage.clear()
})

describe('pendingStoredSession', () => {
  it('reads back what was written', () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })

    expect(readPendingStoredSession()).toEqual({ gatewayModuleId: 'card_gateway', sessionId: 's1' })
  })

  it('answers null when nothing is stored', () => {
    expect(readPendingStoredSession()).toBeNull()
  })

  it('answers null for a damaged entry', () => {
    window.sessionStorage.setItem(PENDING_STORED_SESSION_KEY, '{not json')

    expect(readPendingStoredSession()).toBeNull()
  })

  it('answers null for an entry of the wrong shape', () => {
    window.sessionStorage.setItem(PENDING_STORED_SESSION_KEY, JSON.stringify({ sessionId: 1 }))

    expect(readPendingStoredSession()).toBeNull()
  })

  it('forgets the session when cleared', () => {
    writePendingStoredSession({ gatewayModuleId: 'card_gateway', sessionId: 's1' })

    clearPendingStoredSession()

    expect(readPendingStoredSession()).toBeNull()
  })
})
