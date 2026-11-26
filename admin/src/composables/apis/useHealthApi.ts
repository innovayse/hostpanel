/**
 * Endpoint function for the server's liveness probe.
 *
 * No state lives here; the plugins store polls it while the server restarts. The function rejects with an
 * `ApiError` when the server answers a refusal, and with the fetch's own error while nothing answers at all.
 */

import { useApi } from '@/composables/useApi'
import { HEALTH_PATH } from '@/lib/apiPaths'
import type { HealthDto } from '@/types/health'

/**
 * Endpoint function for the liveness probe.
 *
 * @returns One typed function per endpoint.
 */
export const useHealthApi = () => {
  const { requestRoot } = useApi()

  /**
   * Reads the liveness probe: when the server process started tells whether it was restarted.
   *
   * @returns The probe's answer.
   */
  const getHealth = (): Promise<HealthDto> => requestRoot<HealthDto>(HEALTH_PATH)

  return { getHealth }
}
