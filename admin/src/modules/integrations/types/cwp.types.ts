/**
 * Live CWP server status returned by the server-info endpoint.
 */
export interface CwpServerInfoDto {
  /** True if the API connection succeeded. */
  connected: boolean
  /** Total hosting accounts on the server, null if unavailable. */
  accountsCount: number | null
  /** CWP software version string, null if unavailable. */
  cwpVersion: string | null
  /** ISO 8601 timestamp of the last successful connection test, null if never tested. */
  lastTestedAt: string | null
  /** Human-readable error message if connected is false, otherwise null. */
  errorMessage: string | null
}
