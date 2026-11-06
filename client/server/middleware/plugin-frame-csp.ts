/**
 * Server middleware: narrows `frame-src` on the document of a plugin page.
 *
 * The edges this repository ships keep `frame-src 'self' https:`, because public pages embed operator content and a
 * client's domains cannot be listed statically; an edge with no CSP leaves this header as the only policy. Policies intersect in a browser, so this adds a second header holding only `frame-src`:
 * the origins the API computed for this visitor and this page, and `'none'` whenever anything is unsure (no session, a
 * refused or failed read, an origin of an unexpected shape). A failure never blocks the page, it only closes frames.
 * `'self'` is left out on purpose: nothing on these pages frames the panel.
 *
 * A language prefix (`/hy/client/apps/...`) names the same page, so it gets the same policy. A document that received
 * the API's origins also says so in a `Server-Timing` entry: the preview block cannot read a CSP, and a document served
 * before the visitor signed in stays closed after a sign-in inside it, so the block loads the page once in full then.
 *
 * @module server/middleware/plugin-frame-csp
 */

import type { H3Event } from 'h3'
import { internalApiCall } from '~/server/utils/api'
import { isDocumentNavigation } from '~/server/utils/documentNavigation'
import { PluginBackendPaths } from '~/server/utils/pluginBackendPaths'
import { checkedPluginPagePath } from '~/server/utils/pluginPath'
import { hasShape } from '~/server/utils/requireRouteParam'
import { APP_LOCALES } from '~/constants/appLocales'
import { CLIENT_APPS_PATH } from '~/constants/clientRoutes'
import { PLUGIN_FRAME_TIMING } from '~/constants/pluginFrames'
import { HttpMethod } from '~/types/httpmethod'
import type { PluginPageView } from '~/types/pluginpageview'
import { RouteParamKind } from '~/types/routeparamkind'

/** The policy that frames nothing. */
const FRAME_NOTHING = "frame-src 'none'"

/** What an origin must look like before it is written into a response header. */
const SAFE_ORIGIN = /^https?:\/\/[a-z0-9.\-[\]:]+$/

/**
 * An address without its language prefix: the published languages other than the default keep their pages under one.
 *
 * @param pathname - The requested path.
 * @returns The path the page is known by in every language.
 */
const withoutLocale = (pathname: string): string => {
  const [, first = '', ...rest] = pathname.split('/')
  return (APP_LOCALES as readonly string[]).includes(first) ? `/${rest.join('/')}` : pathname
}

/**
 * The `frame-src` policy of a list of origins.
 *
 * @param origins - The API's frame origins.
 * @returns The policy; frames nothing for an empty list or when any origin has an unexpected shape.
 */
const policyOf = (origins: readonly string[]): string =>
  origins.length > 0 && origins.every(origin => SAFE_ORIGIN.test(origin)) ? `frame-src ${origins.join(' ')}` : FRAME_NOTHING

/**
 * Reads the frame origins of the page an address names.
 *
 * @param event - The incoming request.
 * @param plugin - The plugin id in the address.
 * @param rest - The encoded address below the plugin, without its leading `/`; empty for the root page.
 * @returns The policy to send.
 */
const readPolicy = async (event: H3Event, plugin: string, rest: string): Promise<string> => {
  try {
    if (!hasShape(plugin, RouteParamKind.PluginId)) {
      return FRAME_NOTHING
    }
    const path = checkedPluginPagePath(rest === '' ? '/' : `/${rest.split('/').map(decodeURIComponent).join('/')}`)
    const page = await internalApiCall<PluginPageView>(event, PluginBackendPaths.pageByPath(plugin, path))
    return policyOf(page.frameOrigins ?? [])
  } catch {
    // Any failure closes frames and leaves the page itself alone.
    return FRAME_NOTHING
  }
}

/** Adds the narrowing policy to a GET of a plugin page's document. */
export default defineEventHandler(async (event) => {
  const pathname = withoutLocale((event.path ?? '').split('?')[0] ?? '')
  const prefix = `${CLIENT_APPS_PATH}/`
  if (event.method !== HttpMethod.Get || !pathname.startsWith(prefix)) {
    return
  }
  const [plugin = '', ...rest] = pathname.slice(prefix.length).split('/')
  // Only a real navigation costs an API call; anything else that gets this address is closed without asking.
  const policy = isDocumentNavigation(event) ? await readPolicy(event, plugin, rest.join('/')) : FRAME_NOTHING
  setResponseHeaders(event, policy === FRAME_NOTHING
    ? { 'Content-Security-Policy': policy }
    : { 'Content-Security-Policy': policy, 'Server-Timing': PLUGIN_FRAME_TIMING })
})
