/**
 * Route-parameter guard for the Nitro routes.
 *
 * A `[id]` route file cannot be reached without its segment, but a visitor chooses what the
 * segment holds, and most routes splice it into the path of a backend call. So the parameter is
 * checked against the shape the backend route accepts (an integer, a GUID or an opaque id) and
 * only then handed on, percent-encoded. Anything else is a coded 400 and no sentence: the BFF
 * authors none, the browser renders the generic title, and writing "ID is invalid" here would be
 * a BFF-authored sentence in one language, which the localization rule forbids.
 *
 * @module server/utils/requireRouteParam
 */

import type { H3Event } from 'h3'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { RouteParamKind } from '~/types/routeparamkind'

/** The largest value of a 32-bit signed integer key. */
const MAX_INT_KEY = 2147483647

/** The shape of each parameter kind, other than the integer range which is checked numerically. */
const SHAPES: Record<RouteParamKind, RegExp> = {
  [RouteParamKind.Int]: /^[1-9]\d{0,9}$/,
  [RouteParamKind.Guid]: /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
  [RouteParamKind.Identifier]: /^[A-Za-z0-9_-]{1,128}$/,
  [RouteParamKind.DottedName]: /^[a-z]{1,32}(\.[a-z]{1,32}){1,4}$/,
  // The length is checked by the lookahead: 64 is the backend PluginIdentifier's maximum.
  [RouteParamKind.PluginId]: /^(?=.{1,64}$)[a-z0-9]+(-[a-z0-9]+)*$/,
  [RouteParamKind.PluginWidgetId]: /^[a-z0-9][a-z0-9_-]{0,63}$/
}

/**
 * Whether a value is a positive 32-bit integer key, the `Int` rule, shared with query readers
 * (`server/utils/slotQuery.ts`) so a query id and a route id are checked alike.
 *
 * @param value - The decoded value.
 * @returns True for 1 to 2147483647 written without a sign or leading zero.
 */
export const isPositiveIntKey = (value: string): boolean =>
  SHAPES[RouteParamKind.Int].test(value) && Number(value) <= MAX_INT_KEY

/**
 * Plugin ids the backend keeps for its own endpoints under `/api/client/plugins/` (the index and the slots); a plugin
 * can never take one, so the panel's server refuses them instead of forwarding to an endpoint of another shape.
 */
const RESERVED_PLUGIN_IDS: ReadonlySet<string> = new Set(['ui', 'slots'])

/**
 * Whether a value has the shape of the requested kind.
 *
 * @param value - The decoded route parameter.
 * @param kind - The shape the route accepts.
 * @returns True when the value is well formed for the kind.
 */
export const hasShape = (value: string, kind: RouteParamKind): boolean => {
  if (kind === RouteParamKind.Int) {
    return isPositiveIntKey(value)
  }
  if (kind === RouteParamKind.PluginId && RESERVED_PLUGIN_IDS.has(value)) {
    return false
  }
  return SHAPES[kind].test(value)
}

/**
 * Reads a route parameter, refuses the request when it is absent or malformed, and returns it
 * encoded for use inside a backend path.
 *
 * @param event - H3 event of the incoming request.
 * @param name - The parameter's name as declared in the file name, e.g. `id` for `[id].get.ts`.
 * @param kind - The shape the route accepts; a positive integer when omitted.
 * @returns The validated parameter, percent-encoded (a no-op for every accepted shape, kept so
 *   no later change to a shape can reintroduce raw interpolation).
 * @throws H3Error 400 carrying `data.code = PortalErrorCode.BadRouteParam` and no `statusMessage`.
 */
export const requireRouteParam = (event: H3Event, name: string, kind: RouteParamKind = RouteParamKind.Int): string => {
  const value = getRouterParam(event, name)
  if (!value || !hasShape(value, kind)) {
    throw createError({ statusCode: 400, data: { code: PortalErrorCode.BadRouteParam } })
  }
  return encodeURIComponent(value)
}
