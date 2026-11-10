/**
 * Pinia store of what plugins add to the admin panel: the index behind the Add-ons menu, the plugin
 * page on screen, and the calls and streams that page's blocks make to the plugin's admin routes.
 *
 * The only caller of `usePluginUiApi`. A route call never writes shared state: it hands its outcome back
 * to the block that made it, so a failing block shows its own sentence and the rest of the page is
 * untouched. A stream is read event by event and each event handed to the block as it arrives. Nothing
 * is thrown.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { usePluginUiApi } from '@/composables/apis/usePluginUiApi'
import { errorCode, errorSentence, fieldErrors } from '@/lib/errorHandler'
import { fillPluginPath, pluginMenuLinks, pluginPageValues, pluginRouteMethod, pluginRouteRequest } from '@/lib/pluginView'
import { HttpMethod } from '@/types/http'
import type { ServerSentEvent } from '@/types/http'
import { PluginErrorCode } from '@/types/plugin'
import type { PluginMenuLink, PluginPageViewDto, PluginUiPageDto } from '@/types/pluginui'
import type { PluginCallResult, PluginViewRoute } from '@/types/pluginview'

/** The result of a call the page cannot make: nothing sent, nothing to say. */
const UNAVAILABLE: PluginCallResult = { ok: false, data: null, error: '', fieldErrors: {}, unavailable: true }

/**
 * The result of a call that ended well.
 *
 * @param data - The reply.
 * @returns The outcome.
 */
const succeeded = (data: unknown): PluginCallResult => ({ ok: true, data: data ?? null, error: '', fieldErrors: {}, unavailable: false })

/**
 * The result of a refused or broken call: the API's own sentence and per-field messages.
 *
 * @param cause - What the call threw.
 * @returns The outcome.
 */
const failed = (cause: unknown): PluginCallResult => ({
  ok: false, data: null, error: errorSentence(cause), fieldErrors: fieldErrors(cause), unavailable: false,
})

/** Holds the Add-ons index and the plugin page on screen, and makes the calls a page's blocks ask for. */
export const usePluginUiStore = defineStore('pluginUi', () => {
  const api = usePluginUiApi()

  /** The Add-ons entries, in display order; empty until {@link loadUi} or with no plugin. */
  const menu = ref<PluginMenuLink[]>([])
  /** The pages of the index, which a page link is resolved against. */
  const pages = ref<PluginUiPageDto[]>([])
  /** Whether the index was read. */
  const uiLoaded = ref(false)

  /** The plugin page on screen; null until {@link loadPage} or after a failed read. */
  const page = ref<PluginPageViewDto | null>(null)
  /** True while the page is being read. */
  const pageLoading = ref(false)
  /** The sentence of the last failed page read, the API's own; null when there is none. */
  const pageError = ref<string | null>(null)
  /** Whether the last page read found no page for the address (`PLUGIN_PAGE_NOT_FOUND`). */
  const pageNotFound = ref(false)
  /** The page parameter values read from the address of the page on screen. */
  const pageValues = ref<Record<string, string>>({})
  /** The number of the latest page read; an answer to an older one is dropped. */
  let pageRequest = 0
  /**
   * How many times a call that may change what a plugin reports has started or ended. A view that shares
   * its reads asks again whenever this moved, so it never answers from before a change.
   */
  const writes = ref(0)

  /**
   * Reads the plugin index once, or again when asked to (after a language switch). A failed read leaves
   * the menu without plugin entries: the menu is a way in, never the only one.
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
   * Reads the plugin page an address names. The API matches the address against the plugin's page
   * templates; the parameter values are then read from the address by the template it answered.
   *
   * @param pluginId - The plugin id in the address.
   * @param segments - The decoded address segments after the plugin id.
   * @returns Resolves when the read has ended; the outcome is in the state.
   */
  const loadPage = async (pluginId: string, segments: readonly string[]): Promise<void> => {
    const request = ++pageRequest
    pageLoading.value = true
    pageError.value = null
    pageNotFound.value = false
    page.value = null
    pageValues.value = {}
    // A decoded segment holding `/` cannot travel as one segment: no page can match it.
    if (segments.some((segment) => segment.includes('/'))) {
      pageNotFound.value = true
      pageLoading.value = false
      return
    }
    try {
      const view = await api.fetchPageByPath(pluginId, `/${segments.join('/')}`)
      if (request !== pageRequest) {
        return
      }
      pageValues.value = pluginPageValues(view.path, segments)
      page.value = view
    } catch (cause) {
      if (request !== pageRequest) {
        return
      }
      pageNotFound.value = errorCode(cause) === PluginErrorCode.PageNotFound
      pageError.value = errorSentence(cause)
    } finally {
      if (request === pageRequest) {
        pageLoading.value = false
      }
    }
  }

  /**
   * Calls one of a plugin's JSON admin routes and hands the outcome back.
   *
   * @param pluginId - The plugin id.
   * @param route - The view's reference to the route.
   * @param values - The values its `{param}` segments are filled from.
   * @param body - The JSON body of a POST, PUT or PATCH.
   * @param query - Query parameters.
   * @returns The outcome; never throws.
   */
  const callRoute = async (
    pluginId: string,
    route: PluginViewRoute,
    values: Readonly<Record<string, string>>,
    body?: Record<string, unknown>,
    query: Readonly<Record<string, string>> = {},
  ): Promise<PluginCallResult> => {
    const request = pluginRouteRequest(route, values, query)
    if (request === null) {
      return UNAVAILABLE
    }
    const method = request.method
    const reads = method === HttpMethod.Get
    const sendsBody = !reads && method !== HttpMethod.Delete
    try {
      return succeeded(await api.callRoute(pluginId, method, request.url, sendsBody ? body ?? {} : undefined))
    } catch (cause) {
      return failed(cause)
    } finally {
      // A refused write may have changed something too, so any end of one counts.
      if (!reads) {
        writes.value += 1
      }
    }
  }

  /**
   * Calls one of a plugin's streaming admin routes and hands each event over as it arrives.
   *
   * @param pluginId - The plugin id.
   * @param route - The streaming route; a chat sends with POST only.
   * @param values - The values its `{param}` segments are filled from.
   * @param body - The JSON body, e.g. the chat message.
   * @param onEvent - Called with each event, in order.
   * @param signal - Aborting it closes the stream; the call then ends without a failure.
   * @returns The outcome; `data` is always null, the events went to `onEvent`.
   */
  const streamRoute = async (
    pluginId: string,
    route: PluginViewRoute,
    values: Readonly<Record<string, string>>,
    body: Record<string, unknown>,
    onEvent: (event: ServerSentEvent) => void,
    signal: AbortSignal,
  ): Promise<PluginCallResult> => {
    const path = fillPluginPath(route.path, values)
    if (pluginRouteMethod(route) !== HttpMethod.Post || path === null) {
      return UNAVAILABLE
    }
    // Counted when the stream opens too: a result card is drawn while it is open and must read after the turn began.
    writes.value += 1
    try {
      await api.openStream(pluginId, path, body, onEvent, signal)
      return succeeded(null)
    } catch (cause) {
      return signal.aborted ? succeeded(null) : failed(cause)
    } finally {
      writes.value += 1
    }
  }

  return { menu, pages, uiLoaded, page, pageLoading, pageError, pageNotFound, pageValues, writes, loadUi, loadPage, callRoute, streamRoute }
})
