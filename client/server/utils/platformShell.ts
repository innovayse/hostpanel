/**
 * The platform shell's header widget, served from this portal's own origin.
 *
 * The widget is one script that draws the app launcher and the account control. It treats the
 * origin it was served from as "the portal": every address it builds for switching account, adding
 * an account, the launcher's app list and its configuration starts there. Loaded from the shell's
 * own host, it therefore sent a person who picked another account through the shell's sign-in client
 * and callback instead of this portal's, and they landed on a page that does not exist here.
 *
 * So the script is relayed from this origin (`server/routes/widget/[file].ts`), and the three
 * addresses it then asks of its host are answered here: its configuration, its app list and the
 * account page. This module holds what those routes share: whether the widget is enabled for this
 * deployment, and the cached, bounded read of the shell.
 *
 * @module server/utils/platformShell
 */

import { isHeaderWidgetEnabled } from '~/utils/headerWidget'

/** The `data.code` of the 404 the relay routes answer with when the widget is not served to this deployment. */
export const SHELL_NOT_ENABLED = 'SHELL_NOT_ENABLED'

/** The `data.code` of the 502 answered when the shell cannot be reached and nothing is cached. */
export const SHELL_UNAVAILABLE = 'SHELL_UNAVAILABLE'

/** How long a relayed script is reused before the shell is asked again, in milliseconds. */
export const SHELL_CACHE_MS = 5 * 60 * 1000

/** The longest wait for the shell, in milliseconds. */
export const SHELL_TIMEOUT_MS = 5000

/** The script files the relay serves; any other name is a 404, so the route cannot be aimed at another path. */
export const WIDGET_FILES: ReadonlySet<string> = new Set(['header.js', 'header.v2.js'])

/** A relayed script and when it was read. */
interface CachedScript {
  /** The script source. */
  body: string
  /** Epoch milliseconds when it was read. */
  readAt: number
}

/** Relayed scripts by file name; at most {@link WIDGET_FILES} entries, so a caller cannot grow it. */
const scripts = new Map<string, CachedScript>()

/**
 * Returns the shell's address when the widget is served to this deployment.
 *
 * Uses the same rule as the plugin that adds the script tag and the screens that mount it, so a
 * route never answers for a widget the page does not load.
 *
 * @returns The shell's address without a trailing slash.
 * @throws H3Error 404 carrying `data.code = SHELL_NOT_ENABLED` when the widget is not enabled.
 */
export const requireShellUrl = (): string => {
  const config = useRuntimeConfig()
  const mainUrl = config.public.mainUrl as string | undefined
  if (!mainUrl || !isHeaderWidgetEnabled(config.public.authMode as string | undefined, mainUrl)) {
    throw createError({ statusCode: 404, data: { code: SHELL_NOT_ENABLED } })
  }
  return mainUrl.replace(/\/+$/, '')
}

/**
 * Reads one widget script from the shell, reusing a recent copy.
 *
 * A read that fails falls back to the last copy however old it is, so a short outage of the shell
 * does not take the header away from a portal that has already shown it.
 *
 * @param name - A file name from {@link WIDGET_FILES}.
 * @returns The script source.
 * @throws H3Error 502 carrying `data.code = SHELL_UNAVAILABLE` when the shell cannot be read and no copy exists.
 */
export const readWidgetScript = async (name: string): Promise<string> => {
  const shellUrl = requireShellUrl()
  const cached = scripts.get(name)
  if (cached && Date.now() - cached.readAt < SHELL_CACHE_MS) {
    return cached.body
  }

  try {
    const body = await $fetch<string>(`${shellUrl}/widget/${name}`, {
      responseType: 'text',
      timeout: SHELL_TIMEOUT_MS
    })
    scripts.set(name, { body, readAt: Date.now() })
    return body
  } catch {
    if (cached) {
      return cached.body
    }
    throw createError({ statusCode: 502, data: { code: SHELL_UNAVAILABLE } })
  }
}
