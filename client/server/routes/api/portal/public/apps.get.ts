/**
 * GET /api/portal/public/apps
 *
 * The app list the header widget's launcher shows, read from the platform shell that owns it.
 * Answered with an empty list when the shell cannot be reached, which the widget draws as an empty
 * launcher rather than a broken header.
 *
 * The list is the shell's public one: it is not filtered for the visitor, because the visitor's
 * session here is not one the shell knows. Hiding a tile was never access control.
 *
 * @module server/routes/api/portal/public/apps.get
 */

import { requireShellUrl, SHELL_TIMEOUT_MS } from '~/server/utils/platformShell'

// Annotated: the shell's route has the same path as this one, which the typed `$fetch` would otherwise read as a call to itself.
export default defineEventHandler(async (): Promise<unknown[]> => {
  const shellUrl = requireShellUrl()
  try {
    const apps: unknown = await $fetch<unknown>(`${shellUrl}/api/portal/public/apps`, { timeout: SHELL_TIMEOUT_MS })
    return Array.isArray(apps) ? apps : []
  } catch {
    return []
  }
})
