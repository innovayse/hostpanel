/**
 * Pure helpers of the servers screens: a server's last known state, how full it is and how its account usage reads.
 */
import { ServerState } from '@/types/server'
import type { ServerDto } from '@/types/server'
import { EMPTY_VALUE } from '@/lib/format'
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
 * @returns `count / max`, `count`, or a dash when unknown.
 */
export const serverAccountsLabel = (server: ServerCapacitySource): string => {
  if (server.accountsCount === null) {
    return EMPTY_VALUE
  }
  return server.limits.maxAccounts === null ? `${server.accountsCount}` : `${server.accountsCount} / ${server.limits.maxAccounts}`
}
