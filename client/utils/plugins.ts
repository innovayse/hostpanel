/**
 * Pure helpers of the plugin views: the method of a block's route and filling its path, reading a page's
 * parameters from its address, the address of a linked page, reading a field of a reply, testing a view condition, checking a link against the plugin's allowed origins, formatting a bound value, and reading a server-sent event stream.
 *
 * Nothing here renders markup: every value a plugin sends is text, and the components put it in the page as text.
 *
 * @module utils/plugins
 */

import { PLUGIN_SELECTION_ID_PATTERN, PLUGIN_SELECTION_MAX_ID_LENGTH, PLUGIN_SELECTION_MESSAGE_TYPE, PLUGIN_SELECTION_MESSAGE_VERSION } from '~/constants/pluginSelection'
import type { PluginSelectionMessage } from '~/types/pluginpreviewselection'
import { PluginSelectionEvent } from '~/types/pluginselectionevent'
import { PLUGIN_FRAME_TIMING } from '~/constants/pluginFrames'
import type { Component } from 'vue'
import { AppWindow, BarChart3, Bot, Boxes, Cloud, Globe, LayoutGrid, Mail, MessageSquare, Puzzle, Rocket, Settings, Shield, Sparkles } from 'lucide-vue-next'
import { formatDateTime } from '~/utils/formatDate'
import { clientPluginPagePath } from '~/constants/clientRoutes'
import type { PluginMenuItem } from '~/types/pluginmenuitem'
import type { PluginMenuLink } from '~/types/pluginmenulink'
import type { PluginActionBlock, PluginActionBodySource, PluginActionBodyValue, PluginPageLink } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginBlockTone } from '~/types/pluginblocktone'
import type { PluginChatResult, PluginChatResultAction } from '~/types/pluginchatresult'
import type { PluginChatStep } from '~/types/pluginchatstep'
import { PluginChatStepState } from '~/types/pluginchatstepstate'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import { PluginValueFormat } from '~/types/pluginvalueformat'
import type { PluginUiPage } from '~/types/pluginuipage'
import type { PluginConditionValue, PluginViewCondition } from '~/types/pluginviewcondition'
import type { PluginViewRoute } from '~/types/pluginviewroute'
import { HttpMethod } from '~/types/httpmethod'
import { UiAlertVariant, UiButtonVariant, UiTone } from '~/utils/ui/enums'

/** The most actions a result card shows. */
const MAX_RESULT_ACTIONS = 5

/** The event name of a server-sent event that names none. */
const DEFAULT_EVENT = 'message'

/** The host's fixed icon set a plugin menu item may name; any other name draws the default icon. */
const PLUGIN_ICONS: Readonly<Record<string, Component>> = {
  apps: AppWindow,
  bot: Bot,
  boxes: Boxes,
  chart: BarChart3,
  chat: MessageSquare,
  cloud: Cloud,
  globe: Globe,
  grid: LayoutGrid,
  mail: Mail,
  rocket: Rocket,
  settings: Settings,
  shield: Shield,
  sparkles: Sparkles,
}

/**
 * The icon of a plugin menu item.
 *
 * @param name - The icon name the plugin chose, or null.
 * @returns The icon from the host's set, or the default plugin icon for an unknown or missing name.
 */
export const pluginIcon = (name: string | null | undefined): Component =>
  (name && Object.hasOwn(PLUGIN_ICONS, name) ? PLUGIN_ICONS[name] : undefined) ?? Puzzle

/** The methods a view's route reference may name, as `HttpMethod` values. */
const ROUTE_METHODS: Readonly<Record<string, HttpMethod>> = {
  get: HttpMethod.Get,
  post: HttpMethod.Post,
  put: HttpMethod.Put,
  patch: HttpMethod.Patch,
  delete: HttpMethod.Delete,
}

/**
 * The HTTP method of a view's route reference.
 *
 * @param route - The reference, e.g. `{ method: 'post', path: '/sites/{siteId}/publish' }`.
 * @returns The method, or undefined for a reference that names none the host calls (the block then shows its
 *   "unavailable" note rather than guessing).
 */
export const pluginRouteMethod = (route: PluginViewRoute | undefined): HttpMethod | undefined =>
  route && typeof route.method === 'string' && Object.hasOwn(ROUTE_METHODS, route.method.toLowerCase())
    ? ROUTE_METHODS[route.method.toLowerCase()]
    : undefined

/**
 * The segments of a path template, without the empty ones a leading or doubled `/` leaves.
 *
 * @param path - A template such as `/sites/{siteId}`.
 * @returns Its segments, e.g. `['sites', '{siteId}']`.
 */
const templateSegments = (path: string): string[] => path.split('/').filter(segment => segment !== '')

/**
 * The parameter name of a template segment.
 *
 * @param segment - One segment of a template.
 * @returns The name of a `{name}` segment, or undefined for a literal one.
 */
const parameterName = (segment: string): string | undefined => /^\{([^{}/]+)\}$/.exec(segment)?.[1]

/** What no literal template segment may hold besides a control character: an escape, a query or fragment start, a backslash. */
const UNSAFE_LITERAL_CHARS = /[%?#\\]/

/**
 * Whether a string holds a control character (below U+0020, or U+007F). Compared by code point rather than by a
 * regular expression, which the `no-control-regex` rule refuses, as `utils/returnPath.ts` does.
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
 * Whether a literal template segment is safe to send as written. The template comes from the plugin, and the filled
 * path is appended below the plugin's own prefix: a `..` or `.` segment, an escape that decodes to one, or a `?`/`#`
 * would let a view climb out of that prefix to another plugin or to the portal's own endpoints.
 *
 * @param segment - One literal segment.
 * @returns True when it may be sent unchanged.
 */
const isSafeLiteral = (segment: string): boolean =>
  segment !== '' && segment !== '.' && segment !== '..' && !UNSAFE_LITERAL_CHARS.test(segment) && !hasControlCharacter(segment)

/**
 * Fills a path template's `{param}` segments.
 *
 * @param path - The path template, e.g. `/sites/{siteId}/chat`; `/` alone is the plugin's root.
 * @param values - The values by parameter name.
 * @returns The path without a leading `/`, every value percent-encoded, or null when a parameter has no value or is
 *   `.` or `..`, or a
 *   literal segment is empty, `.`, `..` or holds `%`, `?`, `#`, a backslash or a control character.
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
    // Own keys only: a parameter named like an Object.prototype member (`toString`) must not fill with inherited text.
    const value = Object.hasOwn(values, name) ? values[name] : undefined
    // `.` and `..` survive percent-encoding unchanged and a URL resolves them as dot segments, so they are refused.
    if (value === undefined || value === '' || value === '.' || value === '..') {
      return null
    }
    filled.push(encodeURIComponent(value))
  }
  return filled.join('/')
}

/**
 * Reads a page's parameter values from the address, by the path template the API answered for that address.
 *
 * @param template - The page's path template, e.g. `/sites/{siteId}`.
 * @param segments - The decoded address segments after `/client/apps/<plugin>`.
 * @returns The values by parameter name; empty when the template and the address disagree, which the API's own
 *   match rules out.
 */
export const pluginPageValues = (template: string, segments: readonly string[]): Record<string, string> => {
  const parts = templateSegments(template)
  if (parts.length !== segments.length) {
    return {}
  }
  const values: Record<string, string> = {}
  for (const [index, part] of parts.entries()) {
    const name = parameterName(part)
    const segment = segments[index]
    if (name !== undefined && segment !== undefined && segment !== '') {
      values[name] = segment
    }
  }
  return values
}

/**
 * The address of a plugin page a link opens.
 *
 * @param pages - The pages of the index.
 * @param pluginId - The plugin the link belongs to.
 * @param pageId - The page the link names.
 * @param values - The link's parameter values by name.
 * @returns The page's path below the plugin's prefix, filled and encoded, or null when the page is not in the
 *   reader's index or a parameter has no value.
 */
export const pluginLinkPath = (
  pages: readonly PluginUiPage[],
  pluginId: string,
  pageId: string,
  values: Readonly<Record<string, string>>,
): string | null => {
  const page = pages.find(candidate => candidate.pluginId === pluginId && candidate.id === pageId)
  return page ? fillPluginPath(page.path, values) : null
}

/**
 * Reads a dotted field of a reply.
 *
 * @param data - The reply.
 * @param field - A dotted path, e.g. `site.previewUrl`; the reply itself when absent.
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
 * Whether a view condition holds for a route's reply: the condition's field, read like {@link readPluginField},
 * strictly equals one of its values. A field the reply lacks never holds, not even against `null`.
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
  // Strict on purpose: the view names `"1"` or `1`, and a status of 1 is not the word "1".
  return expected.some(candidate => candidate === value)
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
 * Whether an origin pattern admits an origin: the same scheme, and the same host, or for `*.` any host below it.
 *
 * @param pattern - The allowed origin, e.g. `https://*.example.com`.
 * @param origin - The link's origin.
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

/** A link a plugin may render: inside the panel, or out to one of the plugin's allowed origins. */
export interface PluginLinkTarget {
  /** The address, exactly as checked. */
  href: string
  /** Whether it leaves the panel: opened in a new tab with `noopener noreferrer`. */
  external: boolean
}

/** Control characters and the backslash, which no link may hold (the API's view check refuses them too). */
// eslint-disable-next-line no-control-regex -- the control range is what this refuses
const UNSAFE_LINK_CHARS = /[\u0000-\u001f\u007f\\]/

/** The portal's own API root: a plugin link never points a reader's click at an endpoint. */
const API_ROOT = '/api'

/** A base the URL parser resolves a host-relative address against; only the path it yields is read. */
const RELATIVE_BASE = 'http://h'

/**
 * Whether a host-relative address lands on the portal's API once the browser has resolved it: `..` segments are
 * collapsed by the URL parser and the path decoded once, so `/client/../api/x` and `/%61pi/x` are caught too.
 *
 * @param href - A host-relative address (one leading `/`).
 * @returns True for `/api` and anything below it, in any case; also for a path that cannot be decoded.
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
 * The link a plugin may render for an address: a host-relative path (one leading `/`, never `//`, never `/api` or
 * below it once resolved and decoded), or an absolute https URL without user info on one of the plugin's allowed origins. The API checks the
 * written `href` members at install; this is the client's check for every address, including one read from a route.
 *
 * @param href - The address, as written in the view or read from a route.
 * @param origins - The plugin's allowed origins.
 * @returns The link, or null when the address may not be linked (it is then not rendered at all).
 */
export const pluginLinkTarget = (href: unknown, origins: readonly string[]): PluginLinkTarget | null => {
  if (typeof href !== 'string' || href === '' || UNSAFE_LINK_CHARS.test(href)) {
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
  // https only and no user info: a plain http link would leave the encrypted session for an address anyone on the
  // path can rewrite, and `user@host` is how a link disguises its real host.
  if (url.protocol !== 'https:' || url.username !== '' || url.password !== '') {
    return null
  }
  return origins.some(pattern => originMatches(pattern, url)) ? { href, external: true } : null
}

/**
 * The address a preview block may frame, failing closed. The origin must be an exact member of the page's
 * `frameOrigins`, with no suffix or wildcard match, and never the panel's own: a framed page on the panel's origin
 * could reach the panel. `javascript:` and `data:` have the origin `null`, which no list holds; http passes only when
 * the host listed that origin, which it does in development alone.
 *
 * @param value - The value read from the route.
 * @param frameOrigins - The page's allowed frame origins.
 * @param ownOrigin - The panel's own origin.
 * @returns The normalised address, or null when nothing may be shown.
 */
export const pluginFrameAddress = (value: unknown, frameOrigins: readonly string[], ownOrigin: string): string | null => {
  if (typeof value !== 'string') {
    return null
  }
  let url: URL
  try {
    url = new URL(value)
  } catch {
    return null
  }
  if (url.username !== '' || url.password !== '' || url.origin === ownOrigin || !frameOrigins.includes(url.origin)) {
    return null
  }
  return url.href
}

/** What the preview block reads of the navigation that loaded the document. */
export interface PluginFrameDocument {
  /** The address the document was loaded from. */
  name?: string
  /** The document's `Server-Timing` entries. */
  serverTiming?: readonly { name: string }[]
}

/**
 * Whether this document was served for this very page with the API's frame origins. A CSP belongs to the document, so a
 * router move keeps the policy of the address it was first loaded at: another document's wide one, or a closed one when
 * that address named no page the caller may open, or when the visitor was signed out as it loaded and signed in inside it.
 * Each would leave the frame blank or too open, so the scope is the exact page, and its policy must be the API's.
 *
 * @param document - The navigation entry of the document.
 * @param pagePath - The path of the page on screen.
 * @returns True only when the document was loaded at this path and its server wrote the origins into its policy.
 */
export const pluginDocumentLoaded = (document: PluginFrameDocument | undefined, pagePath: string): boolean => {
  // Its address alone says nothing about the policy: a document opened signed out keeps a closed one after a sign-in in it.
  if (!document?.name || !document.serverTiming?.some(entry => entry.name === PLUGIN_FRAME_TIMING)) {
    return false
  }
  try {
    return new URL(document.name).pathname.replace(/\/+$/, '') === pagePath.replace(/\/+$/, '')
  } catch {
    return false
  }
}

/**
 * Reads a step event: `{ id?, label, state }`.
 *
 * @param data - The event's data.
 * @returns The step, its key the `id` or else the label; null for a malformed event or an unknown state.
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

/** The most lines a result card keeps behind its details switch. */
const MAX_RESULT_DETAILS = 20

/** The most members a result card's action body may hold; the same cap the API sets on a view's `bodySource`. */
const MAX_RESULT_BODY_MEMBERS = 10

/** A member of an action's body: one camelCase JSON name, the API's rule for a view's `bodySource`. */
const BODY_MEMBER = /^[a-z][A-Za-z0-9]{0,63}$/

/**
 * Whether a value may be sent as a member of an action's body.
 *
 * @param value - The value.
 * @returns True for a string, a finite number or a boolean.
 */
const isBodyValue = (value: unknown): value is PluginActionBodyValue =>
  typeof value === 'string' || typeof value === 'boolean' || (typeof value === 'number' && Number.isFinite(value))

/**
 * Builds the body of an action's call from the reply its `bodySource` read.
 *
 * @param source - The body source.
 * @param reply - The reply; undefined until it is read and after a failed read.
 * @returns The body, or null when a member is absent or is no scalar: a call with part of its body must not be sent,
 *   since the route could then not tell what the reader saw.
 */
export const pluginActionBody = (source: PluginActionBodySource, reply: unknown): Record<string, PluginActionBodyValue> | null => {
  const body: Record<string, PluginActionBodyValue> = {}
  for (const [member, field] of Object.entries(source.fields)) {
    const value = readPluginField(reply, field)
    if (!BODY_MEMBER.test(member) || !isBodyValue(value)) {
      return null
    }
    body[member] = value
  }
  return body
}

/**
 * Reads the body of a result card's action. An event is not checked at install as a view is, so its body is checked here.
 *
 * @param body - The action's `body` member.
 * @returns The body, or null for anything but a flat object of scalars under valid names, at most {@link MAX_RESULT_BODY_MEMBERS}.
 */
const readResultBody = (body: unknown): Record<string, PluginActionBodyValue> | null => {
  if (body === null || typeof body !== 'object' || Array.isArray(body)) {
    return null
  }
  const members = Object.entries(body)
  return members.length <= MAX_RESULT_BODY_MEMBERS && members.every(([member, value]) => BODY_MEMBER.test(member) && isBodyValue(value))
    ? Object.fromEntries(members) as Record<string, PluginActionBodyValue>
    : null
}

/**
 * The condition that ends a result action's follow-up; anything of another shape is no condition. An event's `equals`
 * cannot be an own member name of a C# record, so `equalsAny` is accepted as its spelling.
 *
 * @param until - The action's `until` member.
 * @returns The condition, or undefined.
 */
const readResultUntil = (until: unknown): PluginViewCondition | undefined => {
  const route = readPluginField(until, 'route') as PluginViewRoute | undefined
  const field = readPluginField(until, 'field')
  const equals = readPluginField(until, 'equalsAny')
  if (typeof route?.path !== 'string' || typeof route.method !== 'string' || typeof field !== 'string' || !Array.isArray(equals)) {
    return undefined
  }
  return { route: { method: route.method, path: route.path }, field, equals: equals as PluginConditionValue[] }
}

/**
 * Reads one action of a result event. A link passes {@link pluginLinkTarget}; a route is shown only when it is a call
 * the page can make: an action is never a GET, and an unfilled parameter has nothing to send.
 *
 * @param data - The action.
 * @param key - Its place in the card.
 * @param values - The page's parameter values.
 * @param origins - The plugin's allowed link origins.
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
  if (readPluginField(data, 'resend') === true && href === undefined && route === undefined) {
    return typeof label === 'string' && label !== '' ? { key, label, link: null, block: null, resend: true } : null
  }
  // Exactly one of the two: a view cannot leave it to the host which one wins.
  if (typeof label !== 'string' || label === '' || (href === undefined) === (route === undefined)) {
    return null
  }
  if (href !== undefined) {
    const link = pluginLinkTarget(href, origins)
    return link ? { key, label, link, block: null } : null
  }
  const method = pluginRouteMethod(route)
  if (!route || typeof route.path !== 'string' || method === undefined || method === HttpMethod.Get || fillPluginPath(route.path, values) === null) {
    return null
  }
  const declaredBody = readPluginField(data, 'body')
  const body = declaredBody === undefined ? undefined : readResultBody(declaredBody)
  // Dropped whole: a button sent without the body its plugin wrote would act without saying what the reader saw.
  if (body === null) {
    return null
  }
  const confirm = readPluginField(data, 'confirm')
  const until = readResultUntil(readPluginField(data, 'until'))
  const block: PluginActionBlock = {
    kind: PluginBlockKind.Action,
    label,
    route: { method: route.method, path: route.path },
    ...(body ? { body } : {}),
    ...(typeof confirm === 'string' && confirm !== '' ? { confirm } : {}),
    // A card's action changes the same things a turn does, so it reads again what the chat reads again after one.
    ...(refresh.length > 0 ? { refresh: [...refresh] } : {}),
    // The work may run on after the reply; `until` keeps those blocks reading, within the host's own bounds.
    ...(refresh.length > 0 && until ? { followUp: { refresh: [...refresh], until } } : {}),
  }
  return { key, label, link: null, block }
}

/** The tones a result card may carry; any other reads as information. */
const RESULT_TONES: readonly unknown[] = [PluginBlockTone.Info, PluginBlockTone.Success, PluginBlockTone.Warning, PluginBlockTone.Danger]

/**
 * The lines a result card keeps behind its details switch; anything but non-empty text is left out.
 *
 * @param details - The event's `details` member.
 * @returns The lines, at most {@link MAX_RESULT_DETAILS}.
 */
const readResultDetails = (details: unknown): string[] =>
  (Array.isArray(details) ? details : []).filter((line): line is string => typeof line === 'string' && line !== '').slice(0, MAX_RESULT_DETAILS)

/**
 * Reads a result event: `{ tone?, title?, text?, actions? }`.
 *
 * @param data - The event's data.
 * @param values - The page's parameter values, which route actions are filled from.
 * @param origins - The plugin's allowed link origins.
 * @returns The card without its key; null for a malformed event (no title or text, or a member of the wrong type).
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
  return {
    tone: RESULT_TONES.includes(tone) ? tone as PluginBlockTone : PluginBlockTone.Info,
    title,
    text,
    details: readResultDetails(readPluginField(data, 'details')),
    // Cut before filtering: the sixth action is dropped even when an earlier one is refused.
    actions: actions.slice(0, MAX_RESULT_ACTIONS).flatMap((action: unknown, position: number) => {
      const parsed = parseResultAction(action, position, values, origins, refresh)
      return parsed ? [parsed] : []
    }),
  }
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
 * @param locale - The reader's locale.
 * @returns The text; an em dash for a missing value, and JSON for an object no format reads.
 */
export const formatPluginValue = (value: unknown, format: PluginValueFormat | undefined, locale: string): string => {
  if (value === undefined || value === null || value === '') {
    return '—'
  }
  if (format === PluginValueFormat.DateTime && (typeof value === 'string' || typeof value === 'number')) {
    return formatDateTime(value, locale)
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
 * @returns The badge and progress tone.
 */
export const pluginUiTone = (tone: PluginBlockTone | undefined): UiTone => {
  switch (tone) {
    case PluginBlockTone.Info: return UiTone.Info
    case PluginBlockTone.Success: return UiTone.Success
    case PluginBlockTone.Warning: return UiTone.Warning
    case PluginBlockTone.Danger: return UiTone.Danger
    case PluginBlockTone.Primary: return UiTone.Primary
    default: return UiTone.Default
  }
}

/**
 * The alert variant of a plugin tone.
 *
 * @param tone - The plugin's tone.
 * @returns The alert variant; information for a neutral or missing tone.
 */
export const pluginAlertVariant = (tone: PluginBlockTone | undefined): UiAlertVariant => {
  switch (tone) {
    case PluginBlockTone.Success: return UiAlertVariant.Success
    case PluginBlockTone.Warning: return UiAlertVariant.Warning
    case PluginBlockTone.Danger: return UiAlertVariant.Error
    default: return UiAlertVariant.Info
  }
}

/**
 * The button variant of a plugin tone.
 *
 * @param tone - The plugin's tone.
 * @returns The button variant; outline for a neutral or missing tone.
 */
export const pluginButtonVariant = (tone: PluginBlockTone | undefined): UiButtonVariant => {
  switch (tone) {
    case PluginBlockTone.Primary: return UiButtonVariant.Primary
    case PluginBlockTone.Danger: return UiButtonVariant.Danger
    default: return UiButtonVariant.Outline
  }
}

/**
 * Parses one server-sent event block (the lines between two blank lines).
 *
 * @param block - The block's text.
 * @returns The event, or null for a block holding only comments (a heartbeat).
 */
const parseEventBlock = (block: string): PluginStreamEvent | null => {
  let type = DEFAULT_EVENT
  const data: string[] = []
  for (const line of block.split('\n')) {
    if (line === '' || line.startsWith(':')) {
      continue
    }
    const colon = line.indexOf(':')
    const name = colon === -1 ? line : line.slice(0, colon)
    const value = colon === -1 ? '' : line.slice(colon + 1).replace(/^ /, '')
    if (name === 'event') {
      type = value
    } else if (name === 'data') {
      data.push(value)
    }
  }
  if (data.length === 0) {
    return null
  }
  const text = data.join('\n')
  try {
    return { type, data: JSON.parse(text) as unknown }
  } catch {
    return { type, data: text }
  }
}

/** The most characters one unfinished event may hold before the stream is given up: 1 Mi characters. */
export const MAX_PLUGIN_EVENT_CHARS = 1024 * 1024

/** The `data.code` a stream is given up with when one event passes {@link MAX_PLUGIN_EVENT_CHARS}. */
export const PLUGIN_STREAM_EVENT_TOO_LARGE = 'PLUGIN_STREAM_EVENT_TOO_LARGE'

/** The status that refusal carries, the one a broken upstream answer gets everywhere else (502). */
const STREAM_TOO_LARGE_STATUS = 502

/**
 * Reads a server-sent event stream to its end, handing each event over as it arrives.
 *
 * @param stream - The response body.
 * @param onEvent - Called with each event, in order.
 * @param signal - Aborting it stops reading and releases the stream; the promise then resolves.
 * @returns Resolves when the stream ended or was aborted.
 * @throws Whatever reading throws when the connection breaks; the events read before arrived already. An event
 *   longer than {@link MAX_PLUGIN_EVENT_CHARS} cancels the stream and throws `{ statusCode: 502, data: { code:
 *   PLUGIN_STREAM_EVENT_TOO_LARGE } }`.
 */
export const readPluginEventStream = async (
  stream: ReadableStream<Uint8Array>,
  onEvent: (event: PluginStreamEvent) => void,
  signal?: AbortSignal,
): Promise<void> => {
  const reader = stream.getReader()
  const decoder = new TextDecoder()
  /** Cancels the read in flight, so an abort does not wait for the next chunk. */
  const stop = (): void => {
    reader.cancel().catch(() => undefined)
  }
  signal?.addEventListener('abort', stop, { once: true })
  /** The decoded text not yet split into events, line ends already normalised to `\n`. */
  let buffer = ''
  /** A `\r` that ended the last chunk: it may be the first half of a `\r\n` the next chunk completes. */
  let heldReturn = ''
  try {
    while (!signal?.aborted) {
      const { done, value } = await reader.read()
      if (done) {
        break
      }
      // Normalised after joining with what was held back, and a trailing `\r` held back again: a `\r\n` split across
      // two chunks would otherwise count as two line ends and end an event in the middle of it.
      let text = heldReturn + decoder.decode(value, { stream: true })
      heldReturn = text.endsWith('\r') ? '\r' : ''
      text = heldReturn ? text.slice(0, -1) : text
      // Only the new text and the one character before it can complete a `\n\n`, so the scan starts there instead of
      // rereading the whole held event on every chunk.
      const from = Math.max(0, buffer.length - 1)
      buffer += text.replace(/\r\n?/g, '\n')
      let start = 0
      let end = buffer.indexOf('\n\n', from)
      while (end !== -1) {
        const event = parseEventBlock(buffer.slice(start, end))
        if (event && !signal?.aborted) {
          onEvent(event)
        }
        start = end + 2
        end = buffer.indexOf('\n\n', start)
      }
      buffer = buffer.slice(start)
      // One event may not grow without end: a stream that never closes an event would otherwise hold the tab's memory.
      if (buffer.length > MAX_PLUGIN_EVENT_CHARS) {
        stop()
        throw Object.assign(new Error('Plugin stream event too large'), {
          statusCode: STREAM_TOO_LARGE_STATUS,
          data: { code: PLUGIN_STREAM_EVENT_TOO_LARGE },
        })
      }
    }
  } catch (err: unknown) {
    if (!signal?.aborted) {
      throw err
    }
  } finally {
    signal?.removeEventListener('abort', stop)
    reader.releaseLock()
  }
}

/**
 * Resolves the plugin menu items of an index to page addresses, in display order. An item whose page is missing
 * from the index, whose page takes parameters (a menu has no values to fill them with), or whose path fails the
 * `fillPluginPath` segment rule, is left out.
 *
 * @param menus - The index's menu items.
 * @param pages - The index's pages.
 * @returns The entries by `order`, then plugin and item id.
 */
export const pluginMenuLinks = (
  menus: readonly PluginMenuItem[],
  pages: readonly PluginUiPage[],
): PluginMenuLink[] => [...menus]
  .sort((a, b) => a.order - b.order || a.pluginId.localeCompare(b.pluginId) || a.id.localeCompare(b.id))
  .flatMap((item) => {
    const page = pages.find(candidate => candidate.pluginId === item.pluginId && candidate.id === item.page)
    // Filled with no values: a page with parameters, or one whose path would leave the plugin's prefix, gives null.
    const path = page ? fillPluginPath(page.path, {}) : null
    if (path === null) {
      return []
    }
    return [{ label: item.label, icon: item.icon, url: clientPluginPagePath(item.pluginId, path) }]
  })

/**
 * Reads a message from a preview frame, which is a claim from an untrusted page: exactly the fixed members in the fixed
 * shape, the expected release, and an id of the fixed form. Anything else answers null.
 *
 * @param data - What the frame posted.
 * @param expectedRelease - The release the plugin's server says the preview holds.
 * @returns The message, or null when it is refused.
 */
export const parsePluginSelectionMessage = (data: unknown, expectedRelease: string): PluginSelectionMessage | null => {
  if (typeof data !== 'object' || data === null || Array.isArray(data)) {
    return null
  }
  const message = data as Record<string, unknown>
  const members = Object.keys(message).length
  if (message.type !== PLUGIN_SELECTION_MESSAGE_TYPE
    || message.version !== PLUGIN_SELECTION_MESSAGE_VERSION
    || expectedRelease === ''
    || message.release !== expectedRelease) {
    return null
  }
  if (message.event === PluginSelectionEvent.Ready && members === 4) {
    return { event: PluginSelectionEvent.Ready }
  }
  if (message.event === PluginSelectionEvent.Exit && members === 4) {
    return { event: PluginSelectionEvent.Exit }
  }
  if (message.event === PluginSelectionEvent.Toggle && members === 5 && typeof message.id === 'string'
    && message.id.length <= PLUGIN_SELECTION_MAX_ID_LENGTH && PLUGIN_SELECTION_ID_PATTERN.test(message.id)) {
    return { event: PluginSelectionEvent.Toggle, id: message.id }
  }
  return null
}
