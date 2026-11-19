/**
 * Unit tests of the servers helpers: state, load and account usage, for a list row and for a lean option alike, and the facts of a card.
 */
import { describe, expect, it } from 'vitest'
import { EMPTY_VALUE } from '@/lib/format'
import { SERVER_STATE_KEYS, SERVER_STATE_TONES, ServerFactKey, serverAccountsLabel, serverFacts, serverLoadPercent, serverNameServerHosts, serverState } from '@/lib/servers'
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

describe('serverNameServerHosts', () => {
  it('lists the nameserver host names that are set, in order', () => {
    const server = serverDto()
    server.nameServers.ns1Hostname = 'ns1.example.com'
    server.nameServers.ns3Hostname = 'ns3.example.com'

    expect(serverNameServerHosts(server)).toEqual(['ns1.example.com', 'ns3.example.com'])
  })

  it('is empty when none is set', () => {
    expect(serverNameServerHosts(serverDto())).toEqual([])
  })
})

describe('serverFacts', () => {
  it('draws the services, their monthly revenue and the connection of a server with no group, datacenter or nameservers', () => {
    const facts = serverFacts(serverDto())

    expect(facts.map((fact) => fact.key)).toEqual([ServerFactKey.Services, ServerFactKey.Revenue, ServerFactKey.Connection])
    expect(facts.map((fact) => fact.value)).toEqual(['3', '45.00 USD / month', 'API over HTTPS'])
  })

  it('adds the group, datacenter and nameservers when the server has them, in display order', () => {
    const server = serverDto({ serverGroupName: 'Shared', hosting: { datacenter: 'Frankfurt', serverStatusAddress: null, monthlyCost: 20 } })
    server.nameServers.ns1Hostname = 'ns1.example.com'
    server.nameServers.ns2Hostname = 'ns2.example.com'

    const facts = serverFacts(server)

    expect(facts.map((fact) => fact.key)).toEqual([
      ServerFactKey.Group,
      ServerFactKey.Datacenter,
      ServerFactKey.Services,
      ServerFactKey.Revenue,
      ServerFactKey.Connection,
      ServerFactKey.NameServers,
    ])
    expect(facts[0]?.value).toBe('Shared')
    expect(facts[5]?.value).toBe('ns1.example.com, ns2.example.com')
  })

  it('leaves the services and the revenue out when the list did not report them, and names an unencrypted connection', () => {
    const server = serverDto({ usage: null })
    server.endpoint.useSSL = false

    const facts = serverFacts(server)

    expect(facts.map((fact) => fact.key)).toEqual([ServerFactKey.Connection])
    expect(facts[0]?.value).toBe('API over HTTP')
  })

  it('shows a server without active services as zero, not as missing', () => {
    const facts = serverFacts(serverDto({ usage: { activeServices: 0, monthlyRevenue: 0, currency: 'USD' } }))

    expect(facts.find((fact) => fact.key === ServerFactKey.Services)?.value).toBe('0')
    expect(facts.find((fact) => fact.key === ServerFactKey.Revenue)?.value).toBe('0.00 USD / month')
  })
})
