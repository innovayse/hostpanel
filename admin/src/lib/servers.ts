/**
 * Pure helpers of the servers list: a server's last known state, how full it is, and the list narrowed by
 * module, state and search text.
 */
import { ServerState } from '@/types/server'
import type { ServerDto } from '@/types/server'

/** The server list narrowed by these; an empty string means the criterion is not set. */
interface ServerCriteria {
  /** Control panel module the server must run, or an empty string for any. */
  module: string
  /** Last known state the server must be in, or an empty string for any. */
  state: string
  /** Text that must appear in the server's name, hostname or IP address (ignoring case), or an empty string. */
  search: string
}

/**
 * Last known connection state of a server.
 *
 * @param server - The server.
 * @returns Online or offline after a test, untested when it has never been tested.
 */
export const serverState = (server: ServerDto): ServerState => {
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
export const serverLoadPercent = (server: ServerDto): number | null => {
  const max = server.limits.maxAccounts
  if (server.accountsCount === null || max === null || max <= 0) {
    return null
  }
  return (server.accountsCount / max) * 100
}

/**
 * Narrows the servers by module, state and search text.
 *
 * @param servers - The whole list.
 * @param criteria - What a server must match; unset criteria let every server through.
 * @returns The matching servers in their original order.
 */
export const filterServers = (servers: readonly ServerDto[], criteria: ServerCriteria): ServerDto[] => {
  const needle = criteria.search.trim().toLowerCase()
  return servers.filter((server) => {
    if (criteria.module !== '' && server.module !== criteria.module) {
      return false
    }
    if (criteria.state !== '' && serverState(server) !== criteria.state) {
      return false
    }
    if (needle === '') {
      return true
    }
    return [server.name, server.endpoint.hostname, server.endpoint.ipAddress ?? '']
      .some(text => text.toLowerCase().includes(needle))
  })
}
