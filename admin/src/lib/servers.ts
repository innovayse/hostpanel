/**
 * Pure helpers of the servers screens: a server's last known state, how full it is, how its account usage reads and the facts of its card.
 */
import { ServerState, ServersGroupState } from '@/types/server'
import type { ServerDto } from '@/types/server'
import { EMPTY_VALUE, joinParts } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { formatPlainAmount } from '@/lib/money'
import { translate } from '@/lib/translate'
import { UiTone } from '@/utils/ui/tone'

/** What the state helper reads of a server; a list row and a lean option both have it. */
type ServerStateSource = Pick<ServerDto, 'isOnline'>

/** What the load helpers read of a server; a list row and a lean option both have it. */
type ServerCapacitySource = Pick<ServerDto, 'accountsCount' | 'limits'>

/** Locale key of the caption of each server state. */
export const SERVER_STATE_KEYS: Record<ServerState, string> = {
  [ServerState.Online]: 'servers.servers.online',
  [ServerState.Offline]: 'servers.servers.offline',
  [ServerState.Untested]: 'servers.servers.notTested',
}

/** Badge tone of each server state. */
export const SERVER_STATE_TONES: Record<ServerState, UiTone> = {
  [ServerState.Online]: UiTone.Success,
  [ServerState.Offline]: UiTone.Danger,
  [ServerState.Untested]: UiTone.Neutral,
}

/** Locale key of the caption of each state a group of servers stands in. */
export const SERVERS_GROUP_STATE_KEYS: Record<ServersGroupState, string> = {
  [ServersGroupState.Online]: 'servers.servers.online',
  [ServersGroupState.Partial]: 'servers.servers.partiallyOnline',
  [ServersGroupState.Offline]: 'servers.servers.offline',
  [ServersGroupState.Untested]: 'servers.servers.notTested',
}

/** Badge tone of each state a group of servers stands in. */
export const SERVERS_GROUP_STATE_TONES: Record<ServersGroupState, UiTone> = {
  [ServersGroupState.Online]: UiTone.Success,
  [ServersGroupState.Partial]: UiTone.Warning,
  [ServersGroupState.Offline]: UiTone.Danger,
  [ServersGroupState.Untested]: UiTone.Neutral,
}

/**
 * Last known connection state of a server.
 *
 * @param server - The server.
 * @returns Online or offline after a test, untested when it has never been tested.
 */
export const serverState = (server: ServerStateSource): ServerState => {
  if (server.isOnline === true) {
    return ServerState.Online
  }
  return server.isOnline === false ? ServerState.Offline : ServerState.Untested
}

/** How a group of servers stands together: the counts of each last known state and the one state that stands for the group. */
export interface ServersConnectionSummary {
  /** Partial when some servers are online and some offline, offline when every tested one is, online when at least one is and none is offline, untested when none has been tested. */
  state: ServersGroupState
  /** Servers whose last test succeeded. */
  online: number
  /** Servers whose last test failed. */
  offline: number
  /** Servers never tested. */
  untested: number
  /** All the servers counted. */
  total: number
}

/**
 * Decides the one state that stands for a group of servers.
 *
 * @param online - Servers whose last test succeeded.
 * @param offline - Servers whose last test failed.
 * @returns Partial when both kinds are present, otherwise the kind that is, untested when neither is.
 */
const groupState = (online: number, offline: number): ServersGroupState => {
  if (online > 0 && offline > 0) {
    return ServersGroupState.Partial
  }
  if (offline > 0) {
    return ServersGroupState.Offline
  }
  return online > 0 ? ServersGroupState.Online : ServersGroupState.Untested
}

/**
 * Sums up the last known connection state of a group of servers, for a panel plugin whose connection is kept per server.
 *
 * @param servers - The servers, at least one.
 * @returns The counts and the state that stands for the group.
 */
export const summarizeServers = (servers: ServerStateSource[]): ServersConnectionSummary => {
  const states = servers.map(serverState)
  const online = states.filter((state) => state === ServerState.Online).length
  const offline = states.filter((state) => state === ServerState.Offline).length
  return { state: groupState(online, offline), online, offline, untested: servers.length - online - offline, total: servers.length }
}

/**
 * Words the answer of a successful connection test: the state, then the account count and the version when the server reported them.
 *
 * @param result - What the test reported: the account count and the version, each null when the panel gave none.
 * @returns For example "Online · 15 accounts · 7.4.2"; an unknown count or version is left out rather than written as zero or blank.
 */
export const serverOnlineMessage = (result: { accountsCount: number | null; version: string | null }): string =>
  joinParts([
    translate('servers.servers.online'),
    result.accountsCount === null ? null : translate('servers.servers.accountsCount', { count: result.accountsCount }),
    result.version,
  ])

/**
 * How full a server is.
 *
 * @param server - The server.
 * @returns The share of its account capacity in use (percent, may pass 100), or null when the capacity is
 * unlimited or the account count is unknown.
 */
export const serverLoadPercent = (server: ServerCapacitySource): number | null => {
  const max = server.limits.maxAccounts
  if (server.accountsCount === null || max === null || max <= 0) {
    return null
  }
  return (server.accountsCount / max) * 100
}

/**
 * Formats the account usage of a server.
 *
 * @param server - The server.
 * @returns `count / max`, `count / Unlimited` for a server with no limit, a dash when the count is unknown, or just "Unlimited"
 * when it is neither known nor limited.
 */
export const serverAccountsLabel = (server: ServerCapacitySource): string => {
  const max = server.limits.maxAccounts
  const limit = max === null ? translate('servers.serverForm.unlimited') : String(max)
  if (server.accountsCount === null) {
    return max === null ? limit : EMPTY_VALUE
  }
  return `${server.accountsCount} / ${limit}`
}

/** The facts a server card shows beside its name, in display order. */
export enum ServerFactKey {
  /** The group the server belongs to. */
  Group = 'group',
  /** The datacenter the server is hosted in. */
  Datacenter = 'datacenter',
  /** How many active services run on the server. */
  Services = 'services',
  /** What those services renew for each month. */
  Revenue = 'revenue',
  /** How the panel reaches the server's API. */
  Connection = 'connection',
  /** The nameservers the server's accounts are delegated to. */
  NameServers = 'nameServers',
}

/** How many fact placeholders the server card skeleton draws; the real count follows the data a server has. */
export const SERVER_FACT_SLOTS = 4

/** One fact of a server card: what it is, its icon and its already translated caption and value. */
export interface ServerFact {
  /** Which fact it is; part of its test id. */
  key: ServerFactKey
  /** Inner SVG markup of its icon. */
  icon: string
  /** Translated caption. */
  label: string
  /** Translated, formatted values, one per line; a single-valued fact has one. */
  values: string[]
}

/** Locale key of the caption of each server fact. */
const SERVER_FACT_LABEL_KEYS: Record<ServerFactKey, string> = {
  [ServerFactKey.Group]: 'servers.servers.factGroup',
  [ServerFactKey.Datacenter]: 'servers.servers.factDatacenter',
  [ServerFactKey.Services]: 'servers.servers.factServices',
  [ServerFactKey.Revenue]: 'servers.servers.factRevenue',
  [ServerFactKey.Connection]: 'servers.servers.factConnection',
  [ServerFactKey.NameServers]: 'servers.servers.factNameServers',
}

/**
 * The nameserver host names a server's accounts are delegated to.
 *
 * @param server - The server.
 * @returns The host names that are set, in order; empty when none is.
 */
export const serverNameServerHosts = (server: Pick<ServerDto, 'nameServers'>): string[] => {
  const ns = server.nameServers
  return [ns.ns1Hostname, ns.ns2Hostname, ns.ns3Hostname, ns.ns4Hostname, ns.ns5Hostname].filter((host): host is string => Boolean(host))
}

/**
 * The value list of a single-valued fact.
 *
 * @param value - The value, or null/empty when the server has no data for the fact.
 * @returns One value, or none.
 */
const valuesOf = (value: string | null): string[] => (value ? [value] : [])

/**
 * The facts a server card shows: group, datacenter, services and their monthly revenue, how the API is reached and the
 * nameservers. A fact the server has no data for is left out, never filled with a made-up value.
 *
 * @param server - The server, as the list returns it.
 * @returns The facts, in display order.
 */
export const serverFacts = (server: ServerDto): ServerFact[] => {
  const nameServers = serverNameServerHosts(server)
  const facts: [ServerFactKey, string, string[]][] = [
    [ServerFactKey.Group, Icon.Folder, valuesOf(server.serverGroupName)],
    [ServerFactKey.Datacenter, Icon.Rack, valuesOf(server.hosting.datacenter)],
    [ServerFactKey.Services, Icon.Box, valuesOf(server.usage ? String(server.usage.activeServices) : null)],
    [
      ServerFactKey.Revenue,
      Icon.Dollar,
      valuesOf(server.usage ? translate('servers.servers.perMonth', { amount: formatPlainAmount(server.usage.monthlyRevenue, server.usage.currency) }) : null),
    ],
    [
      ServerFactKey.Connection,
      server.endpoint.useSSL ? Icon.ShieldCheck : Icon.ShieldOff,
      [translate(server.endpoint.useSSL ? 'servers.servers.connectionHttps' : 'servers.servers.connectionHttp')],
    ],
    [ServerFactKey.NameServers, Icon.Globe, nameServers],
  ]
  return facts.flatMap(([key, icon, values]) => (values.length > 0 ? [{ key, icon, label: translate(SERVER_FACT_LABEL_KEYS[key]), values }] : []))
}
