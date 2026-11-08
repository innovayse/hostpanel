/**
 * Pinia store of the client area's plugins: the plugin index behind the menu, the plugin page on
 * screen, the slots plugins fill on host pages, and the calls the blocks make to the plugin's routes.
 *
 * A route call never writes shared state: it hands its result back to the block that made it, so a
 * failing block shows its own note and the rest of the page is untouched. A stream is read here,
 * event by event, and each event handed to the block as it arrives.
 *
 * @module stores/plugins
 */

import { defineStore } from 'pinia'
import { usePluginsApi } from '~/composables/apis/usePluginsApi'
import type { PluginMenuLink } from '~/types/pluginmenulink'
import { apiErrorCode, apiErrorMessage, apiFieldErrors, PortalErrorCode } from '~/lib/errorHandler'
import { HttpMethod } from '~/types/httpmethod'
import type { PluginCallResult } from '~/types/plugincallresult'
import type { PluginPageView } from '~/types/pluginpageview'
import type { PluginSlotId } from '~/types/pluginslotid'
import type { PluginSlotWidget } from '~/types/pluginslotwidget'
import type { PluginSlotWidgetView } from '~/types/pluginslotwidgetview'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import type { PluginUiPage } from '~/types/pluginuipage'
import type { PluginViewRoute } from '~/types/pluginviewroute'
import { fillPluginPath, pluginMenuLinks, pluginPageValues, pluginRouteMethod, pluginRouteRequest, readPluginEventStream } from '~/utils/plugins'

/** The result of a call the page cannot make: nothing sent, nothing to say. */
const UNAVAILABLE: PluginCallResult = { ok: false, data: null, error: '', fieldErrors: {}, unavailable: true }

/**
 * The plugin store.
 */
export const usePluginsStore = defineStore('plugins', () => {
  const api = usePluginsApi()

  /** The plugin menu links the signed-in person may open, in display order; empty until {@link loadUi} or with no plugin. */
  const menu = ref<PluginMenuLink[]>([])
  /** The plugin pages the signed-in person may open, which a page link is resolved against. */
  const pages = ref<PluginUiPage[]>([])
  /** Whether the index was read. */
  const uiLoaded = ref(false)

  /** The plugin page on screen; null until {@link loadPage} or after a failed read. */
  const page = ref<PluginPageView | null>(null)
  /** True while the page is being read. */
  const pageLoading = ref(false)
  /** The sentence for the last failed page read, the API's `detail`; empty when there is none. */
  const pageError = ref('')
  /** Whether the last page read was refused because no such page exists for the reader (`PLUGIN_PAGE_NOT_FOUND`). */
  const pageNotFound = ref(false)
  /** The page parameter values read from the address of the page on screen. */
  const pageValues = ref<Record<string, string>>({})
  /** The number of the latest page read; an answer to an older one is dropped. */
  let pageRequest = 0
  /**
   * How many times a call that may change what a plugin reports has started or ended (every call that is not a read
   * ends one, and every stream starts one and ends one). A view that shares reads asks for a new one whenever this
   * moved, so it never answers from before a change: a stream counts when it opens as well, because the cards it draws
   * read while it is still open and must not be shown the picture from before the turn.
   */
  const writes = ref(0)

  /**
   * Reads the plugin index once per session, or again when asked to.
   *
   * A failed read leaves the menu without plugin items: the menu is a way in, never the only one,
   * and a refusal here (an identity with no client record) is explained by the pages themselves.
   *
   * @param force - Read again even when read before.
   * @returns Resolves when the read has ended.
   */
  const loadUi = async (force = false): Promise<void> => {
    if (uiLoaded.value && !force) {
      return
    }
    try {
      const ui = await api.fetchUi()
      pages.value = ui.pages
      menu.value = pluginMenuLinks(ui.menus, ui.pages)
    } catch {
      pages.value = []
      menu.value = []
    }
    uiLoaded.value = true
  }

  /**
   * Reads the plugin page an address names. The API matches the address against the plugin's page templates, so a missing page is its `PLUGIN_PAGE_NOT_FOUND` with its own
   * sentence; the parameter values are then read from the address by the template it answered.
   *
   * @param pluginId - The plugin id in the address.
   * @param segments - The decoded address segments after the plugin id.
   * @returns Resolves when the read has ended; the outcome is in the state.
   */
  const loadPage = async (pluginId: string, segments: readonly string[]): Promise<void> => {
    // Only the latest read may write the page: a reader who moves on before a slow answer arrives must not see the
    // page they left replace the one they opened.
    const request = ++pageRequest
    pageLoading.value = true
    pageError.value = ''
    pageNotFound.value = false
    page.value = null
    pageValues.value = {}
    // A decoded segment holding `/` cannot travel as one segment of the address: no page can match it.
    if (segments.some(segment => segment.includes('/'))) {
      pageNotFound.value = true
      pageLoading.value = false
      return
    }
    try {
      // Decoded: the address is a query value, encoded once by the transport; encoding it here too would make
      // `/կայք` arrive at the matcher as `/%D5%AF…`.
      const view = await api.fetchPageByPath(pluginId, `/${segments.join('/')}`)
      if (request !== pageRequest) {
        return
      }
      pageValues.value = pluginPageValues(view.path, segments)
      page.value = view
    } catch (err: unknown) {
      if (request !== pageRequest) {
        return
      }
      // A malformed address (`BAD_ROUTE_PARAM`, refused by the panel's own server) names no page either.
      const code = apiErrorCode(err)
      pageNotFound.value = code === PortalErrorCode.PluginPageNotFound || code === PortalErrorCode.BadRouteParam
      pageError.value = apiErrorMessage(err)
    } finally {
      if (request === pageRequest) {
        pageLoading.value = false
      }
    }
  }

  /**
   * Forgets everything read for the signed-in person: the index (so the next person's menu is read afresh), the page
   * on screen and its values. A page read still out is dropped when it answers.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    pageRequest++
    menu.value = []
    pages.value = []
    uiLoaded.value = false
    page.value = null
    pageLoading.value = false
    pageError.value = ''
    pageNotFound.value = false
    pageValues.value = {}
  }

  /**
   * Reads the widget index of one slot. A slot is an addition to a host page, never a part it needs,
   * so a failed read shows nothing and leaves the page as it would be without plugins; no sentence is
   * shown, so there is no error state to keep.
   *
   * @param slotId - The slot id.
   * @param context - The slot's context, e.g. `{ serviceId: '4' }`.
   * @returns The widgets; empty on a failure or with no plugin.
   */
  const loadSlot = async (slotId: PluginSlotId, context: Record<string, string>): Promise<PluginSlotWidget[]> => {
    try {
      return (await api.fetchSlot(slotId, context)).widgets
    } catch {
      return []
    }
  }

  /**
   * Reads one slot widget's view. A failure is the widget's own neutral note, never the page's error.
   *
   * @param slotId - The slot id.
   * @param pluginId - The plugin id.
   * @param widgetId - The widget id.
   * @param context - The slot's context.
   * @returns The widget with its view; null when it could not be read.
   */
  const loadSlotWidget = async (
    slotId: PluginSlotId,
    pluginId: string,
    widgetId: string,
    context: Record<string, string>,
  ): Promise<PluginSlotWidgetView | null> => {
    try {
      return await api.fetchSlotWidget(slotId, pluginId, widgetId, context)
    } catch {
      return null
    }
  }

  /**
   * Calls one of a plugin's JSON routes and hands the outcome back.
   *
   * @param pluginId - The plugin id.
   * @param route - The view's reference to the route.
   * @param values - The values its `{param}` segments are filled from.
   * @param body - The JSON body of a POST, PUT or PATCH.
   * @param query - Query parameters, e.g. a table's page.
   * @returns The outcome; never throws.
   */
  const callRoute = async (
    pluginId: string,
    route: PluginViewRoute,
    values: Record<string, string>,
    body?: Record<string, unknown>,
    query: Record<string, string> = {},
  ): Promise<PluginCallResult> => {
    const request = pluginRouteRequest(route, values, query)
    if (request === null) {
      return UNAVAILABLE
    }
    const reads = request.method === HttpMethod.Get
    const sendsBody = request.method !== HttpMethod.Get && request.method !== HttpMethod.Delete
    try {
      const data = await api.callRoute(pluginId, request.method, request.url, sendsBody ? body ?? {} : undefined)
      return { ok: true, data: data ?? null, error: '', fieldErrors: {}, unavailable: false }
    } catch (err: unknown) {
      return { ok: false, data: null, error: apiErrorMessage(err), fieldErrors: apiFieldErrors(err), unavailable: false }
    } finally {
      // A refused write may have changed something too, so any end of one counts.
      if (!reads) {
        writes.value += 1
      }
    }
  }

  /**
   * Calls one of a plugin's streaming routes and hands each event over as it arrives.
   *
   * @param pluginId - The plugin id.
   * @param route - The view's reference to the streaming route; a chat sends with POST only.
   * @param values - The values its `{param}` segments are filled from.
   * @param body - The JSON body, e.g. the chat message.
   * @param onEvent - Called with each event, in order.
   * @param signal - Aborting it closes the stream; the call then ends without a failure.
   * @returns The outcome: the refusal's or broken connection's sentence in `error`, `unavailable` for a route the
   *   page cannot call, and `ok` when the stream ended or was aborted. `data` is always null; the events went to
   *   `onEvent`.
   */
  const streamRoute = async (
    pluginId: string,
    route: PluginViewRoute,
    values: Record<string, string>,
    body: Record<string, unknown>,
    onEvent: (event: PluginStreamEvent) => void,
    signal: AbortSignal,
  ): Promise<PluginCallResult> => {
    const path = fillPluginPath(route.path, values)
    if (pluginRouteMethod(route) !== HttpMethod.Post || path === null) {
      return UNAVAILABLE
    }
    // Counted before the first event, not only when the stream ends: a result card is drawn while the stream is open and
    // reads its condition then, which has to be a read made after the turn began.
    writes.value += 1
    try {
      await readPluginEventStream(await api.openStream(pluginId, path, body, signal), onEvent, signal)
      return { ok: true, data: null, error: '', fieldErrors: {}, unavailable: false }
    } catch (err: unknown) {
      if (signal.aborted) {
        return { ok: true, data: null, error: '', fieldErrors: {}, unavailable: false }
      }
      return { ok: false, data: null, error: apiErrorMessage(err), fieldErrors: apiFieldErrors(err), unavailable: false }
    } finally {
      // A stream is a turn of work the plugin did, however it ended; the state it left is read after it ended too.
      writes.value += 1
    }
  }

  return { menu, pages, uiLoaded, page, pageLoading, pageError, pageNotFound, pageValues, writes, loadUi, loadPage, reset, loadSlot, loadSlotWidget, callRoute, streamRoute }
})
