/**
 * Pure helpers of the plugin views in the admin panel: the method of a block's route and filling its
 * path, reading a page's parameters from its address, reading a field of a reply, testing a view
 * condition, checking a link against the plugin's allowed origins, formatting a bound value, mapping a
 * plugin tone to the kit, reading chat events, resolving the Add-ons menu and its icons.
 *
 * Nothing here renders markup: every value a plugin sends is text, and the components put it in the
 * page as text. The bounds below are the host's, never the plugin's.
 */

import { formatDateTime } from '@/lib/format'
import { pluginPagePath } from '@/lib/routes'
import { HttpMethod } from '@/types/http'
import type { PluginMenuLink, PluginUiMenuItemDto, PluginUiPageDto } from '@/types/pluginui'
import { PluginBlockKind, PluginBlockTone, PluginChatStepState, PluginValueFormat } from '@/types/pluginview'
import type {
  PluginActionBlock,
  PluginActionBodyValue,
  PluginChatResult,
  PluginChatResultAction,
  PluginChatStep,
  PluginConditionValue,
  PluginLinkTarget,
  PluginPageLink,
  PluginRouteRequest,
  PluginViewCondition,
  PluginViewRoute,
} from '@/types/pluginview'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'

/** How often a follow-up reads its condition, in milliseconds. */
export const PLUGIN_FOLLOW_UP_INTERVAL_MS = 3000

/** The most reads one follow-up makes (about a minute at the interval above). */
export const PLUGIN_FOLLOW_UP_MAX_ATTEMPTS = 20

/** How long the reads of a view are shared before a reader asks again, in milliseconds. */
export const PLUGIN_VIEW_READ_MAX_AGE_MS = 10_000

/** The most characters a condition's dotted field may have. */
const CONDITION_MAX_FIELD_LENGTH = 200

/** The most values a condition may compare against. */
const CONDITION_MAX_VALUES = 20

/** A dotted field path: segments of letters, digits, `_` and `-`. */
const CONDITION_FIELD_PATTERN = /^[A-Za-z0-9_-]+(\.[A-Za-z0-9_-]+)*$/

/** The most actions a result card shows. */
const MAX_RESULT_ACTIONS = 5

/** The most lines a result card keeps behind its details switch. */
const MAX_RESULT_DETAILS = 20

/** The most members a result card's action body may hold. */
const MAX_RESULT_BODY_MEMBERS = 10

/** A member of an action's body: one camelCase JSON name. */
const BODY_MEMBER = /^[a-z][A-Za-z0-9]{0,63}$/

/** The methods a view's route reference may name. */
const ROUTE_METHODS: Readonly<Record<string, HttpMethod>> = {
  get: HttpMethod.Get,
  post: HttpMethod.Post,
  put: HttpMethod.Put,
  patch: HttpMethod.Patch,
  delete: HttpMethod.Delete,
}

/**
 * Path data of the icons a plugin menu item may name, drawn by the sidebar like its own entries. Any
 * other name draws {@link DEFAULT_PLUGIN_ICON}; a name is never loaded as an address.
 */
const PLUGIN_ICONS: Readonly<Record<string, string>> = {
  apps: 'M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z',
  bot: 'M12 8V4H8M4 8h16v12H4zM2 14h2M20 14h2M15 13v2M9 13v2',
  boxes: 'M21 16V8l-9-5-9 5v8l9 5 9-5zM3.3 7L12 12l8.7-5M12 22V12',
  chart: 'M18 20V10M12 20V4M6 20v-6',
  chat: 'M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z',
  cloud: 'M18 10h-1.26A8 8 0 109 20h9a5 5 0 000-10z',
  globe: 'M12 2a10 10 0 100 20 10 10 0 000-20zM2 12h20M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z',
  grid: 'M3 3h18v18H3zM3 9h18M3 15h18M9 3v18M15 3v18',
  mail: 'M4 4h16a2 2 0 012 2v12a2 2 0 01-2 2H4a2 2 0 01-2-2V6a2 2 0 012-2zM22 6l-10 7L2 6',
  rocket: 'M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 00-2.91-.09zM12 15l-3-3a22 22 0 012-3.95A12.88 12.88 0 0122 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 01-4 2z',
  settings: 'M12 15a3 3 0 100-6 3 3 0 000 6zM19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-2.82 1.17V21a2 2 0 01-4 0v-.09a1.65 1.65 0 00-2.82-1.17l-.06.06a2 2 0 11-2.83-2.83l.06-.06A1.65 1.65 0 003 15.4H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 112.83-2.83l.06.06A1.65 1.65 0 009 4.6V3a2 2 0 014 0v.09a1.65 1.65 0 002.82 1.17l.06-.06a2 2 0 112.83 2.83l-.06.06A1.65 1.65 0 0019.4 9H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 2z',
  shield: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
  sparkles: 'M12 3l1.9 5.8L20 10l-6.1 1.9L12 18l-1.9-6.1L4 10l6.1-1.2zM19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z',
}

/** Path data of the default plugin icon: a puzzle piece. */
export const DEFAULT_PLUGIN_ICON =
  'M19.4 13a2 2 0 100-4H18V6a2 2 0 00-2-2h-3V2.6a2 2 0 10-4 0V4H6a2 2 0 00-2 2v3h1.4a2 2 0 110 4H4v5a2 2 0 002 2h5v-1.4a2 2 0 114 0V20h1a2 2 0 002-2v-5z'

/**
 * The icon path data of a plugin menu item.
 *
 * @param name - The icon name the plugin chose, or null.
 * @returns The icon from the host's set, or {@link DEFAULT_PLUGIN_ICON} for an unknown or missing name.
 */
export const pluginIconPath = (name: string | null | undefined): string =>
  (name && Object.hasOwn(PLUGIN_ICONS, name) ? PLUGIN_ICONS[name] : undefined) ?? DEFAULT_PLUGIN_ICON

/**
 * The HTTP method of a view's route reference.
 *
 * @param route - The reference, e.g. `{ method: 'post', path: '/sites/{siteId}/publish' }`.
 * @returns The method, or undefined for a reference that names none the panel calls.
 */
export const pluginRouteMethod = (route: PluginViewRoute | undefined): HttpMethod | undefined =>
  route && typeof route.method === 'string' && Object.hasOwn(ROUTE_METHODS, route.method.toLowerCase())
    ? ROUTE_METHODS[route.method.toLowerCase()]
    : undefined

/**
 * The parameter name of a template segment.
 *
 * @param segment - One segment of a template.
 * @returns The name of a `{name}` segment, or undefined for a literal one.
 */
const parameterName = (segment: string): string | undefined => /^\{([^{}/]+)\}$/.exec(segment)?.[1]

/** What no literal template segment may hold besides a control character. */
const UNSAFE_LITERAL_CHARS = /[%?#\\]/

/**
 * Whether a string holds a control character (below U+0020, or U+007F), compared by code point.
 *
 * @param value - The string to scan.
 * @returns True when any character is a control character.
 */
const hasControlCharacter = (value: string): boolean =>
  Array.from(value).some((character) => {
    const code = character.charCodeAt(0)
    return code < 0x20 || code === 0x7f
  })

/**
 * Whether a literal template segment is safe to send as written: a dot segment, an escape, a query or
 * fragment start would let a view climb out of the plugin's prefix.
 *
 * @param segment - One literal segment.
 * @returns True when it may be sent unchanged.
 */
const isSafeLiteral = (segment: string): boolean =>
  segment !== '' && segment !== '.' && segment !== '..' && !UNSAFE_LITERAL_CHARS.test(segment) && !hasControlCharacter(segment)

/**
 * Fills a path template's `{param}` segments.
 *
 * @param path - The template, e.g. `/sites/{siteId}/chat`; `/` alone is the plugin's root.
 * @param values - The values by parameter name.
 * @returns The path without a leading `/`, every value percent-encoded, or null when a parameter has no
 *   value or is `.` or `..`, or a literal segment is unsafe.
 */
export const fillPluginPath = (path: string, values: Readonly<Record<string, string>>): string | null => {
  const rest = path.startsWith('/') ? path.slice(1) : path
  if (rest === '') {
    return ''
  }
  const filled: string[] = []
  for (const segment of rest.split('/')) {
    const name = parameterName(segment)
    if (name === undefined) {
      if (!isSafeLiteral(segment)) {
        return null
      }
      filled.push(segment)
      continue
    }
    // Own keys only: a parameter named like an Object.prototype member must not fill with inherited text.
    const value = Object.hasOwn(values, name) ? values[name] : undefined
    if (value === undefined || value === '' || value === '.' || value === '..') {
      return null
    }
    filled.push(encodeURIComponent(value))
  }
  return filled.join('/')
}

/**
 * The request a route reference comes to for a page: the one place a reference becomes a call, so the
 * store that sends it and the view that shares reads of it agree on what "the same request" is.
 *
 * @param route - The view's reference to the route.
 * @param values - The values its `{param}` segments are filled from.
 * @param query - Query parameters.
 * @returns The request, or null for a reference the page cannot call.
 */
export const pluginRouteRequest = (
  route: PluginViewRoute,
  values: Readonly<Record<string, string>>,
  query: Readonly<Record<string, string>> = {},
): PluginRouteRequest | null => {
  const method = pluginRouteMethod(route)
  const path = fillPluginPath(route.path, values)
  if (method === undefined || path === null) {
    return null
  }
  const search = new URLSearchParams(query).toString()
  return { method, url: search ? `${path}?${search}` : path }
}

/**
 * Reads a page's parameter values from the address, by the path template the API answered for it.
 *
 * @param template - The page's path template, e.g. `/sites/{siteId}`.
 * @param segments - The decoded address segments after `/apps/<plugin>`.
 * @returns The values by parameter name; empty when the template and the address disagree.
 */
export const pluginPageValues = (template: string, segments: readonly string[]): Record<string, string> => {
  const parts = template.split('/').filter((segment) => segment !== '')
  if (parts.length !== segments.length) {
    return {}
  }
  const values: Record<string, string> = {}
  parts.forEach((part, index) => {
    const name = parameterName(part)
    const segment = segments[index]
    if (name !== undefined && segment !== undefined && segment !== '') {
      values[name] = segment
    }
  })
  return values
}

/**
 * Reads a dotted field of a reply.
 *
 * @param data - The reply.
 * @param field - A dotted path, e.g. `site.status`; the reply itself when absent.
 * @returns The value, or undefined when the path leads nowhere.
 */
export const readPluginField = (data: unknown, field: string | undefined): unknown => {
  if (!field) {
    return data
  }
  let current: unknown = data
  for (const key of field.split('.')) {
    if (current === null || typeof current !== 'object' || !Object.hasOwn(current, key)) {
      return undefined
    }
    current = (current as Record<string, unknown>)[key]
  }
  return current
}

/**
 * Whether a view condition holds for a route's reply: its field strictly equals one of its values. A
 * field the reply lacks never holds, not even against `null`.
 *
 * @param condition - The condition.
 * @param data - The reply of the condition's route.
 * @returns True when the field equals one of the values.
 */
export const pluginConditionHolds = (condition: PluginViewCondition, data: unknown): boolean => {
  const value = readPluginField(data, condition.field)
  if (value === undefined) {
    return false
  }
  const expected: readonly PluginConditionValue[] = Array.isArray(condition.equals) ? condition.equals : [condition.equals]
  // Strict on purpose: a status of 1 is not the word "1".
  return expected.some((candidate) => candidate === value)
}

/**
 * The values of a page link's parameters, read from a row or a reply.
 *
 * @param link - The link.
 * @param source - The row or reply the fields are read from.
 * @returns The values by parameter name, or null when one is missing.
 */
export const pluginLinkParams = (link: PluginPageLink, source: unknown): Record<string, string> | null => {
  const values: Record<string, string> = {}
  for (const [name, field] of Object.entries(link.params ?? {})) {
    const value = readPluginField(source, field)
    if (value === undefined || value === null || value === '') {
      return null
    }
    values[name] = String(value)
  }
  return values
}

/**
 * Whether an origin pattern admits an origin: the same scheme and port, and the same host, or for `*.` any
 * host below it.
 *
 * @param pattern - The allowed origin, e.g. `https://*.example.com`.
 * @param origin - The link's address.
 * @returns True when admitted.
 */
const originMatches = (pattern: string, origin: URL): boolean => {
  let allowed: URL
  try {
    allowed = new URL(pattern.replace('://*.', '://wildcard.'))
  } catch {
    return false
  }
  if (allowed.protocol !== origin.protocol || allowed.port !== origin.port) {
    return false
  }
  if (pattern.includes('://*.')) {
    const suffix = allowed.hostname.slice('wildcard'.length)
    return origin.hostname.endsWith(suffix) && origin.hostname.length > suffix.length
  }
  return allowed.hostname === origin.hostname
}

/**
 * Whether a string holds a control character or a backslash, which no link may hold.
 *
 * @param value - The address.
 * @returns True when it does.
 */
const hasUnsafeLinkCharacter = (value: string): boolean => value.includes('\\') || hasControlCharacter(value)

/** The host's API root: a plugin link never points a click at an endpoint. */
const API_ROOT = '/api'

/** A base the URL parser resolves a host-relative address against; only the path it yields is read. */
const RELATIVE_BASE = 'http://h'

/**
 * Whether a host-relative address lands on the API once resolved: `..` segments collapsed and the path
 * decoded once, so `/x/../api/y` and `/%61pi/y` are caught too.
 *
 * @param href - A host-relative address.
 * @returns True for `/api` and below, in any case; also for a path that cannot be decoded.
 */
const reachesApi = (href: string): boolean => {
  let path: string
  try {
    path = decodeURIComponent(new URL(href, RELATIVE_BASE).pathname).toLowerCase()
  } catch {
    return true
  }
  return path === API_ROOT || path.startsWith(`${API_ROOT}/`)
}

/**
 * The link a plugin may render for an address: a host-relative path (one leading `/`, never `//`, never
 * `/api` or below), or an absolute https URL without user info on one of the plugin's allowed origins.
 * The API checks the written members at install; this is the panel's check for every address, including
 * one read from a route.
 *
 * @param href - The address.
 * @param origins - The plugin's allowed origins (`externalOrigins` of the page).
 * @returns The link, or null when the address may not be linked (it is then not rendered at all).
 */
export const pluginLinkTarget = (href: unknown, origins: readonly string[]): PluginLinkTarget | null => {
  if (typeof href !== 'string' || href === '' || hasUnsafeLinkCharacter(href)) {
    return null
  }
  if (href.startsWith('/')) {
    return href.startsWith('//') || reachesApi(href) ? null : { href, external: false }
  }
  let url: URL
  try {
    url = new URL(href)
  } catch {
    return null
  }
  // https only and no user info: `user@host` is how a link disguises its real host.
  if (url.protocol !== 'https:' || url.username !== '' || url.password !== '') {
    return null
  }
  return origins.some((pattern) => originMatches(pattern, url)) ? { href, external: true } : null
}

/**
 * Whether a value may be sent as a member of an action's body.
 *
 * @param value - The value.
 * @returns True for a string, a finite number or a boolean.
 */
const isBodyValue = (value: unknown): value is PluginActionBodyValue =>
  typeof value === 'string' || typeof value === 'boolean' || (typeof value === 'number' && Number.isFinite(value))

/**
 * Reads the body of a result card's action, which no install check has seen.
 *
 * @param body - The action's `body` member.
 * @returns The body, or null for anything but a flat object of scalars under valid names.
 */
const readResultBody = (body: unknown): Record<string, PluginActionBodyValue> | null => {
  if (body === null || typeof body !== 'object' || Array.isArray(body)) {
    return null
  }
  const members = Object.entries(body)
  return members.length <= MAX_RESULT_BODY_MEMBERS && members.every(([member, value]) => BODY_MEMBER.test(member) && isBodyValue(value))
    ? (Object.fromEntries(members) as Record<string, PluginActionBodyValue>)
    : null
}

/**
 * Whether a value is one JSON scalar a condition may compare against.
 *
 * @param value - The value.
 * @returns True for a string, a finite number, a boolean or null.
 */
const isConditionValue = (value: unknown): value is PluginConditionValue =>
  value === null || typeof value === 'string' || typeof value === 'boolean' || (typeof value === 'number' && Number.isFinite(value))

/**
 * Reads a condition a plugin wrote into a chat event, held to the view's own bounds: a GET route the
 * page can fill, a dotted field, and 1 to 20 scalars under `equalsAny`.
 *
 * @param condition - The condition member of the event.
 * @param values - The page's parameter values.
 * @returns The condition, or undefined when it is of another shape.
 */
const readResultCondition = (condition: unknown, values: Readonly<Record<string, string>>): PluginViewCondition | undefined => {
  const route = readPluginField(condition, 'route') as PluginViewRoute | undefined
  const field = readPluginField(condition, 'field')
  const equals = readPluginField(condition, 'equalsAny')
  if (
    typeof route?.path !== 'string' || typeof route.method !== 'string' || pluginRouteMethod(route) !== HttpMethod.Get
    || fillPluginPath(route.path, values) === null
    || typeof field !== 'string' || field.length > CONDITION_MAX_FIELD_LENGTH || !CONDITION_FIELD_PATTERN.test(field)
    || !Array.isArray(equals) || equals.length === 0 || equals.length > CONDITION_MAX_VALUES || !equals.every(isConditionValue)
  ) {
    return undefined
  }
  return { route: { method: route.method, path: route.path }, field, equals }
}

/**
 * Reads one action of a result event: a link through {@link pluginLinkTarget}, a call the page can make
 * (never a GET), or a resend. A `visibleWhen` that cannot be read drops the action whole.
 *
 * @param data - The action.
 * @param key - Its place in the card.
 * @param values - The page's parameter values.
 * @param origins - The plugin's allowed link origins.
 * @param refresh - The block ids the chat reads again after a turn.
 * @returns The action, or null when it is not to be rendered.
 */
const parseResultAction = (
  data: unknown,
  key: number,
  values: Readonly<Record<string, string>>,
  origins: readonly string[],
  refresh: readonly string[],
): PluginChatResultAction | null => {
  const label = readPluginField(data, 'label')
  const href = readPluginField(data, 'href')
  const route = readPluginField(data, 'route') as PluginViewRoute | undefined
  const declaredVisibleWhen = readPluginField(data, 'visibleWhen')
  const visibleWhen = declaredVisibleWhen === undefined ? undefined : readResultCondition(declaredVisibleWhen, values)
  if (declaredVisibleWhen !== undefined && !visibleWhen) {
    return null
  }
  const shown = visibleWhen ? { visibleWhen } : {}
  if (readPluginField(data, 'resend') === true && href === undefined && route === undefined) {
    return typeof label === 'string' && label !== '' ? { key, label, link: null, block: null, resend: true, ...shown } : null
  }
  // Exactly one of the two: a card cannot leave it to the panel which one wins.
  if (typeof label !== 'string' || label === '' || (href === undefined) === (route === undefined)) {
    return null
  }
  if (href !== undefined) {
    const link = pluginLinkTarget(href, origins)
    return link ? { key, label, link, block: null, ...shown } : null
  }
  const method = pluginRouteMethod(route)
  if (!route || typeof route.path !== 'string' || method === undefined || method === HttpMethod.Get || fillPluginPath(route.path, values) === null) {
    return null
  }
  const declaredBody = readPluginField(data, 'body')
  const body = declaredBody === undefined ? undefined : readResultBody(declaredBody)
  if (body === null) {
    return null
  }
  const confirm = readPluginField(data, 'confirm')
  const until = readResultCondition(readPluginField(data, 'until'), values)
  const block: PluginActionBlock = {
    kind: PluginBlockKind.Action,
    label,
    route: { method: route.method, path: route.path },
    ...(body ? { body } : {}),
    ...(typeof confirm === 'string' && confirm !== '' ? { confirm } : {}),
    ...(refresh.length > 0 ? { refresh: [...refresh] } : {}),
    ...(refresh.length > 0 && until ? { followUp: { refresh: [...refresh], until } } : {}),
  }
  return { key, label, link: null, block, ...shown }
}

/** The tones a result card may carry; any other reads as information. */
const RESULT_TONES: readonly unknown[] = [PluginBlockTone.Info, PluginBlockTone.Success, PluginBlockTone.Warning, PluginBlockTone.Danger]

/**
 * Reads a result event: `{ tone?, title?, text?, details?, actions? }`.
 *
 * @param data - The event's data.
 * @param values - The page's parameter values.
 * @param origins - The plugin's allowed link origins.
 * @param refresh - The block ids the chat reads again after a turn.
 * @returns The card without its key; null for a malformed event.
 */
export const parsePluginChatResult = (
  data: unknown,
  values: Readonly<Record<string, string>>,
  origins: readonly string[],
  refresh: readonly string[] = [],
): Omit<PluginChatResult, 'key'> | null => {
  const title = readPluginField(data, 'title') ?? ''
  const text = readPluginField(data, 'text') ?? ''
  const actions = readPluginField(data, 'actions') ?? []
  if (typeof title !== 'string' || typeof text !== 'string' || (title === '' && text === '') || !Array.isArray(actions)) {
    return null
  }
  const tone = readPluginField(data, 'tone')
  const details = readPluginField(data, 'details')
  return {
    tone: RESULT_TONES.includes(tone) ? (tone as PluginBlockTone) : PluginBlockTone.Info,
    title,
    text,
    details: (Array.isArray(details) ? details : [])
      .filter((line): line is string => typeof line === 'string' && line !== '')
      .slice(0, MAX_RESULT_DETAILS),
    // Cut before filtering: the sixth action is dropped even when an earlier one is refused.
    actions: actions.slice(0, MAX_RESULT_ACTIONS).flatMap((action: unknown, position: number) => {
      const parsed = parseResultAction(action, position, values, origins, refresh)
      return parsed ? [parsed] : []
    }),
  }
}

/**
 * Reads a step event: `{ id?, label, state }`.
 *
 * @param data - The event's data.
 * @returns The step, keyed by its `id` or else its label; null for a malformed event or an unknown state.
 */
export const parsePluginChatStep = (data: unknown): PluginChatStep | null => {
  const label = readPluginField(data, 'label')
  const id = readPluginField(data, 'id')
  const state = readPluginField(data, 'state')
  if (typeof label !== 'string' || label === '' || (id !== undefined && (typeof id !== 'string' || id === ''))) {
    return null
  }
  if (!(Object.values(PluginChatStepState) as unknown[]).includes(state) || state === PluginChatStepState.Stopped) {
    return null
  }
  return { key: id ?? label, label, state: state as PluginChatStepState }
}

/** A money value as a plugin sends it. */
interface PluginMoney {
  /** The amount. */
  amount: number
  /** The ISO 4217 currency code. */
  currency: string
}

/**
 * Whether a value is a money value.
 *
 * @param value - The value.
 * @returns True for `{ amount: number, currency: string }`.
 */
const isMoney = (value: unknown): value is PluginMoney =>
  value !== null && typeof value === 'object'
  && typeof (value as PluginMoney).amount === 'number' && typeof (value as PluginMoney).currency === 'string'

/**
 * Formats a value bound from a route as text.
 *
 * @param value - The value.
 * @param format - How to format it.
 * @param locale - The operator's locale.
 * @returns The text; an em dash for a missing value, and JSON for an object no format reads.
 */
export const formatPluginValue = (value: unknown, format: PluginValueFormat | undefined, locale: string): string => {
  if (value === undefined || value === null || value === '') {
    return '—'
  }
  if (format === PluginValueFormat.DateTime && typeof value === 'string') {
    return formatDateTime(value)
  }
  if (format === PluginValueFormat.Money && isMoney(value)) {
    try {
      return new Intl.NumberFormat(locale, { style: 'currency', currency: value.currency }).format(value.amount)
    } catch {
      return `${value.amount} ${value.currency}`
    }
  }
  return typeof value === 'object' ? JSON.stringify(value) : String(value)
}

/**
 * The kit tone of a plugin tone.
 *
 * @param tone - The plugin's tone.
 * @returns The badge, alert and card tone; neutral for a missing tone.
 */
export const pluginUiTone = (tone: PluginBlockTone | undefined): UiTone => {
  switch (tone) {
    case PluginBlockTone.Info: {
      return UiTone.Info
    }
    case PluginBlockTone.Success: {
      return UiTone.Success
    }
    case PluginBlockTone.Warning: {
      return UiTone.Warning
    }
    case PluginBlockTone.Danger: {
      return UiTone.Danger
    }
    case PluginBlockTone.Primary: {
      return UiTone.Primary
    }
    default: {
      return UiTone.Neutral
    }
  }
}

/**
 * The alert tone of a plugin tone: information for a neutral or missing one.
 *
 * @param tone - The plugin's tone.
 * @returns The alert tone.
 */
export const pluginAlertTone = (tone: PluginBlockTone | undefined): UiTone =>
  tone === PluginBlockTone.Success || tone === PluginBlockTone.Warning || tone === PluginBlockTone.Danger ? pluginUiTone(tone) : UiTone.Info

/**
 * The button variant of a plugin tone.
 *
 * @param tone - The plugin's tone.
 * @returns Primary or danger for those tones, secondary otherwise.
 */
export const pluginButtonVariant = (tone: PluginBlockTone | undefined): UiButtonVariant => {
  if (tone === PluginBlockTone.Primary) {
    return UiButtonVariant.Primary
  }
  return tone === PluginBlockTone.Danger ? UiButtonVariant.Danger : UiButtonVariant.Secondary
}

/**
 * Resolves the menu items of an index to Add-ons entries, in display order. An item whose page is missing,
 * takes parameters (a menu has no values to fill them with) or fails the segment rule is left out.
 *
 * @param menus - The index's menu items.
 * @param pages - The index's pages.
 * @returns The entries by `order`, then plugin and item id.
 */
export const pluginMenuLinks = (menus: readonly PluginUiMenuItemDto[], pages: readonly PluginUiPageDto[]): PluginMenuLink[] =>
  [...menus]
    .sort((a, b) => a.order - b.order || a.pluginId.localeCompare(b.pluginId) || a.id.localeCompare(b.id))
    .flatMap((item) => {
      const page = pages.find((candidate) => candidate.pluginId === item.pluginId && candidate.id === item.page)
      const path = page ? fillPluginPath(page.path, {}) : null
      if (path === null) {
        return []
      }
      return [{ key: `${item.pluginId}:${item.id}`, label: item.label, icon: item.icon, to: pluginPagePath(item.pluginId, path) }]
    })
