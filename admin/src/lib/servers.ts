/**
 * Pure helpers of the servers screens: a server's last known state, how full it is, how its account usage reads and the facts of its card.
 */
import { ServerState } from '@/types/server'
import type { ServerDto } from '@/types/server'
import { EMPTY_VALUE } from '@/lib/format'
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
