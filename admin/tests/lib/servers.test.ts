/**
 * Unit tests of the servers helpers: state, load and account usage, for a list row and for a lean option alike.
 */
import { describe, expect, it } from 'vitest'
import { EMPTY_VALUE } from '@/lib/format'
import { SERVER_STATE_KEYS, SERVER_STATE_TONES, serverAccountsLabel, serverLoadPercent, serverState } from '@/lib/servers'
import { ServerState } from '@/types/server'
import { serverDto, serverOptionDto } from '../helpers/serverFixtures'

describe('serverState', () => {
  it('maps the last connection result to a state', () => {
    expect(serverState(serverDto({ isOnline: true }))).toBe(ServerState.Online)
    expect(serverState(serverDto({ isOnline: false }))).toBe(ServerState.Offline)
    expect(serverState(serverDto({ isOnline: null }))).toBe(ServerState.Untested)
  })

  it('reads a lean option the same way', () => {
    expect(serverState(serverOptionDto({ isOnline: false }))).toBe(ServerState.Offline)
  })
})

describe('serverLoadPercent', () => {
  it('is the share of capacity in use', () => {
    expect(serverLoadPercent(serverDto({ accountsCount: 25 }))).toBe(25)
  })

  it('is null for unlimited capacity or an unknown count', () => {
    expect(serverLoadPercent(serverDto({ limits: { maxAccounts: null, isDefault: false, isDisabled: false } }))).toBeNull()
    expect(serverLoadPercent(serverDto({ accountsCount: null }))).toBeNull()
  })

  it('reads a lean option the same way, past a full server too', () => {
    expect(serverLoadPercent(serverOptionDto({ accountsCount: 30, limits: { maxAccounts: 120, isDefault: false, isDisabled: false } }))).toBe(25)
    expect(serverLoadPercent(serverOptionDto({ accountsCount: 130, limits: { maxAccounts: 100, isDefault: false, isDisabled: false } }))).toBe(130)
  })
})

describe('serverAccountsLabel', () => {
  it('writes count over limit, the count alone, or a dash when the count is unknown', () => {
    expect(serverAccountsLabel(serverDto({ accountsCount: 12 }))).toBe('12 / 100')
    expect(serverAccountsLabel(serverDto({ accountsCount: 12, limits: { maxAccounts: null, isDefault: false, isDisabled: false } }))).toBe('12')
    expect(serverAccountsLabel(serverDto({ accountsCount: null }))).toBe(EMPTY_VALUE)
  })
})

describe('server state vocabulary', () => {
  it('has a caption key and a tone for every state', () => {
    for (const state of Object.values(ServerState)) {
      expect(SERVER_STATE_KEYS[state]).toMatch(/^servers\.servers\./)
      expect(SERVER_STATE_TONES[state]).toBeTruthy()
    }
  })
})
