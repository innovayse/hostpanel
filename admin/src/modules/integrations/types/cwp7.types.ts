/**
 * Live CWP7 server status returned by the server-info endpoint.
 */
export interface Cwp7ServerInfoDto {
  /** True if the API connection succeeded. */
  connected: boolean
  /** Total hosting packages on the server, null if unavailable. */
  packagesCount: number | null
  /** ISO 8601 timestamp of the last successful connection test, null if never tested. */
  lastTestedAt: string | null
  /** Human-readable error message if connected is false, otherwise null. */
  errorMessage: string | null
}
