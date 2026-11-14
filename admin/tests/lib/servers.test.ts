/**
 * Unit tests of the servers list helpers: state, load and narrowing.
 */
import { describe, expect, it } from 'vitest'
import { EMPTY_VALUE } from '@/lib/format'
import { filterServers, SERVER_STATE_KEYS, SERVER_STATE_TONES, serverAccountsLabel, serverLoadPercent, serverState } from '@/lib/servers'
import { ServerState } from '@/types/server'
import { serverDto } from '../helpers/serverFixtures'

describe('serverState', () => {
  it('maps the last connection result to a state', () => {
    expect(serverState(serverDto({ isOnline: true }))).toBe(ServerState.Online)
    expect(serverState(serverDto({ isOnline: false }))).toBe(ServerState.Offline)
    expect(serverState(serverDto({ isOnline: null }))).toBe(ServerState.Untested)
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
})

describe('filterServers', () => {
  const servers = [
    serverDto({ id: 1, name: 'alpha', module: 'Cwp7', isOnline: true }),
    serverDto({ id: 2, name: 'beta', module: 'Plesk', isOnline: false }),
  ]

  it('lets everything through when nothing is set', () => {
    expect(filterServers(servers, { module: '', state: '', search: '' })).toHaveLength(2)
  })

  it('narrows by module, state and search text', () => {
    expect(filterServers(servers, { module: 'Plesk', state: '', search: '' }).map(s => s.id)).toEqual([2])
    expect(filterServers(servers, { module: '', state: ServerState.Online, search: '' }).map(s => s.id)).toEqual([1])
    expect(filterServers(servers, { module: '', state: '', search: 'ALP' }).map(s => s.id)).toEqual([1])
    expect(filterServers(servers, { module: '', state: '', search: 'host1.example' })).toHaveLength(2)
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
