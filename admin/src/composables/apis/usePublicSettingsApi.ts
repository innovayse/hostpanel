/**
 * The one endpoint the admin panel may read before anybody has signed in.
 *
 * No state lives here — the branding store owns that. This file only says what the
 * backend offers and in what shape.
 *
 * `GET /api/settings/public` is `[AllowAnonymous]` and returns an allow-listed subset of
 * the settings table. That matters here: the panel needs the operator's logo on the
 * login screen, which is by definition rendered with no session, so the branding cannot
 * come from any of the admin-only endpoints.
 */

import { useApi } from '../useApi'
import type { PublicSetting } from '../../types/branding'

/**
 * Endpoint functions for `/api/settings/public`.
 *
 * Rejects with an `ApiError` carrying the response body on failure, like every other
 * API file here; deciding that branding is not worth failing over is the store's call,
 * not this layer's.
 *
 * @returns One typed function per public-settings endpoint the admin panel uses.
 */
export const usePublicSettingsApi = () => {
  const { request } = useApi()

  /**
   * Reads the settings a signed-out page is allowed to see.
   *
   * Anonymous, and has to be — the login screen renders before any session exists and
   * still has to show the operator's mark rather than the product's.
   *
   * @returns The allow-listed key/value pairs that currently have a value.
   */
  const fetchPublicSettings = (): Promise<PublicSetting[]> =>
    request<PublicSetting[]>('/settings/public')

  return { fetchPublicSettings }
}
