/**
 * The wiring of a rendered plugin view: the view hands its context (page values, origins, refresh
 * signals) to the blocks below it, and a block reads its source route through the plugin store.
 *
 * The context is rendering wiring, not business data, so it travels by provide/inject; every call
 * still goes through `stores/plugins.ts`.
 *
 * @module composables/usePluginView
 */

import type { InjectionKey, Ref } from 'vue'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginSource } from '~/types/pluginblock'
import { HttpMethod } from '~/types/httpmethod'
import type { PluginViewContext } from '~/types/pluginviewcontext'
import { pluginRouteMethod, readPluginField } from '~/utils/plugins'

/** The injection key of the view context. */
const PLUGIN_VIEW_KEY: InjectionKey<PluginViewContext> = Symbol('plugin-view')

/**
 * Hands a view's context to the blocks rendered below the calling component.
 *
 * @param context - The view's context.
 * @returns Nothing.
 */
export const providePluginView = (context: PluginViewContext): void => {
  provide(PLUGIN_VIEW_KEY, context)
}

/**
 * Reads the context of the view the calling block is rendered in.
 *
 * @returns The context.
 * @throws Error when the block is rendered outside a plugin view, which is a host bug.
 */
export const usePluginView = (): PluginViewContext => {
  const context = inject(PLUGIN_VIEW_KEY, null)
  if (!context) {
    throw new Error('A plugin block is rendered outside a plugin view.')
  }
  return context
}

/** What {@link usePluginSource} answers. */
export interface PluginSourceState {
  /** The value read: the reply, or its `field`; undefined until read or after a failure. */
  value: Ref<unknown>
  /** True while reading. */
  loading: Ref<boolean>
  /** The sentence of a failed read, the API's; empty when there is none. */
  error: Ref<string>
  /** The source names a route the page cannot read (not a GET, or a parameter the address lacks): render nothing. */
  unavailable: Ref<boolean>
  /** Reads again. */
  reload: () => Promise<void>
}

/**
 * Reads a block's source route when the block mounts, again whenever an action asks the block to
 * refresh, and again when the query changes.
 *
 * A source naming a route that is not a GET reads nothing and reports `unavailable`, with no sentence: the API
 * refuses such a view at install, so this is a guard, and the client has no sentence of its own to give. A route the
 * reader may not call is refused by the API with its own sentence.
 *
 * @param source - The block's source; nothing is read while it is undefined.
 * @param blockId - The block's id, which actions name to refresh it.
 * @param query - Query parameters sent with the read, e.g. a table's page.
 * @returns The value, loading flag, error sentence, unavailable flag and a reload action.
 */
export const usePluginSource = (
  source: () => PluginSource | undefined,
  blockId: () => string | undefined,
  query: () => Record<string, string> = () => ({}),
): PluginSourceState => {
  const context = usePluginView()
  const store = usePluginsStore()

  /** The value read. */
  const value = ref<unknown>(undefined)
  /** True while reading. */
  const loading = ref(false)
  /** The sentence of a failed read. */
  const error = ref('')
  /** Whether the source cannot be read on this page. */
  const unavailable = ref(false)
  /** The number of the latest read; an answer to an older one is dropped. */
  let latest = 0

  /**
   * Reads the source once.
   *
   * @returns Resolves when the read has ended.
   */
  const reload = async (): Promise<void> => {
    // A refresh or a new page of a table can start while the last read is still out; only the latest may write.
    const request = ++latest
    const current = source()
    if (!current) {
      return
    }
    if (pluginRouteMethod(current.route) !== HttpMethod.Get) {
      unavailable.value = true
      return
    }
    loading.value = true
    const result = await store.callRoute(context.pluginId, current.route, context.values, undefined, query())
    if (request !== latest) {
      return
    }
    loading.value = false
    unavailable.value = result.unavailable
    error.value = result.error
    value.value = result.ok ? readPluginField(result.data, current.field) : undefined
  }

  // The first read is reported to the view, so a slot widget can hold its time limit until the blocks have data.
  onMounted(() => {
    context.track(reload())
  })
  watch(() => {
    const id = blockId()
    return id ? context.refreshes.value[id] ?? 0 : 0
  }, reload)
  watch(query, reload, { deep: true })

  return { value, loading, error, unavailable, reload }
}
