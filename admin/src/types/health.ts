/**
 * Shape of the answer of the server's liveness probe (GET /health, outside the API base, anonymous).
 */

/** What the liveness probe answers with. */
export interface HealthDto {
  /** Health status word the server reports. */
  status: string
  /** ISO 8601 UTC instant the answer was written. */
  timestamp: string
  /** ISO 8601 UTC instant the server process started; it changes on every start, so a different value means a restarted process. */
  processStartedAt: string
}
