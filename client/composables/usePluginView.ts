/**
 * The wiring of a rendered plugin view: the view hands its context (page values, origins, refresh
 * signals) to the blocks below it, and a block reads its source route through the plugin store. A
 * block's `visibleWhen` condition and an action's or form's `followUp` are read the same way.
 *
 * The context is rendering wiring, not business data, so it travels by provide/inject; every call
 * still goes through `stores/plugins.ts`.
 *
 * @module composables/usePluginView
 */

import type { ComputedRef, InjectionKey, Ref } from 'vue'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS, PLUGIN_FOLLOW_UP_MAX_ATTEMPTS } from '~/constants/pluginFollowUp'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginSource } from '~/types/pluginblock'
import type { PluginFollowUp } from '~/types/pluginfollowup'
import { HttpMethod } from '~/types/httpmethod'
import type { PluginViewCondition } from '~/types/pluginviewcondition'
import type { PluginViewContext } from '~/types/pluginviewcontext'
import { pluginConditionHolds, pluginRouteMethod, readPluginField } from '~/utils/plugins'

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

/**
 * Whether a block shows, by its `visibleWhen` condition: read when the block mounts and again whenever an action asks
 * the block's own id to refresh, so a follow-up can reveal or hide it.
 *
 * A block with no condition always shows and reads nothing. A block with one is hidden while the condition is being
 * read, when the read fails or is unavailable, and when the field is absent or matches none of the values: it shows
 * only once the condition is known to hold, so a create form never flashes up for a site that already exists.
 *
 * @param condition - The block's condition; undefined for none.
 * @param blockId - The block's id, which actions name to refresh it.
 * @returns Whether the block shows.
 */
export const usePluginCondition = (
  condition: () => PluginViewCondition | undefined,
  blockId: () => string | undefined,
): ComputedRef<boolean> => {
  const context = usePluginView()
  const store = usePluginsStore()

  /** Whether the latest read found the condition holding. */
  const holds = ref(false)
  /** The number of the latest read; an answer to an older one is dropped. */
  let latest = 0

  /**
   * Reads the condition once.
   *
   * @returns Resolves when the read has ended.
   */
  const reload = async (): Promise<void> => {
    const request = ++latest
    const current = condition()
    if (!current) {
      return
    }
    // The API refuses a condition on a route that is not a GET at install; this only guards, and hides the block.
    if (pluginRouteMethod(current.route) !== HttpMethod.Get) {
      holds.value = false
      return
    }
    const result = await store.callRoute(context.pluginId, current.route, context.values)
    if (request !== latest) {
      return
    }
    holds.value = result.ok && pluginConditionHolds(current, result.data)
  }

  onMounted(() => {
    if (condition()) {
      context.track(reload())
    }
  })
  watch(() => {
    const id = blockId()
    return id ? context.refreshes.value[id] ?? 0 : 0
  }, reload)

  return computed(() => !condition() || holds.value)
}

/**
 * Runs an action's or a form's `followUp` after its call succeeded: every {@link PLUGIN_FOLLOW_UP_INTERVAL_MS} it
 * reads the condition's route and asks the named blocks to read again, and it stops once the condition holds (after
 * that last refresh), after {@link PLUGIN_FOLLOW_UP_MAX_ATTEMPTS} reads, or when the calling block unmounts, which is
 * also what leaving the page does. A failed read counts as an attempt and the watch goes on.
 *
 * The interval and the number of attempts are the host's: a view names what to watch and never how often, so no
 * plugin can make the panel poll faster or longer. Starting again cancels a follow-up still running, so two clicks
 * never run two watches.
 *
 * @returns Starts a follow-up; undefined starts nothing.
 */
export const usePluginFollowUp = (): ((followUp: PluginFollowUp | undefined) => void) => {
  const context = usePluginView()
  const store = usePluginsStore()

  /** The pending wait before the next attempt; undefined while none is pending. */
  let timer: ReturnType<typeof setTimeout> | undefined
  /** The number of the current follow-up; a read answering for an older one, or after unmount, is ignored. */
  let run = 0

  /**
   * Ends the running follow-up, if any: the pending wait is cleared and a read still out is ignored when it answers.
   *
   * @returns Nothing.
   */
  const stop = (): void => {
    run += 1
    if (timer !== undefined) {
      clearTimeout(timer)
      timer = undefined
    }
  }

  /**
   * One attempt: reads the condition, refreshes the named blocks and waits for the next attempt unless it is over.
   *
   * @param current - The follow-up this attempt belongs to.
   * @param followUp - What to watch and refresh.
   * @param attempt - The attempt's number, from 1.
   * @returns Resolves when the attempt has ended.
   */
  const attemptOnce = async (current: number, followUp: PluginFollowUp, attempt: number): Promise<void> => {
    timer = undefined
    const result = await store.callRoute(context.pluginId, followUp.until.route, context.values)
    if (current !== run || result.unavailable) {
      return
    }
    const done = result.ok && pluginConditionHolds(followUp.until, result.data)
    // Refreshed after the read, so the final value the condition saw is also what the blocks show.
    context.refresh([...followUp.refresh])
    if (done || attempt >= PLUGIN_FOLLOW_UP_MAX_ATTEMPTS) {
      return
    }
    timer = setTimeout(() => {
      void attemptOnce(current, followUp, attempt + 1)
    }, PLUGIN_FOLLOW_UP_INTERVAL_MS)
  }

  onUnmounted(stop)

  return (followUp: PluginFollowUp | undefined): void => {
    stop()
    // A condition on a route that is not a GET is refused at install and could never end the watch: not started.
    if (!followUp || pluginRouteMethod(followUp.until.route) !== HttpMethod.Get) {
      return
    }
    const current = run
    timer = setTimeout(() => {
      void attemptOnce(current, followUp, 1)
    }, PLUGIN_FOLLOW_UP_INTERVAL_MS)
  }
}

/** The sentence an action answered with, as the place that shows it receives it. */
export interface PluginActionMessage {
  /** The sentence: the plugin's own on a success, the API's on a refusal. */
  text: string
  /** Whether the call failed, which decides the tone. */
  failed: boolean
}

/**
 * Where an action reports its sentence when something above it shows the sentences of a row of actions: the header
 * draws one line under its whole action row, because a sentence drawn under one button grows that button's column
 * and pulls the row out of line.
 */
export type PluginActionMessageOutlet = (message: PluginActionMessage) => void

/** The injection key of the action message outlet. */
const PLUGIN_ACTION_MESSAGE_KEY: InjectionKey<PluginActionMessageOutlet> = Symbol('plugin-action-message')

/**
 * Makes the calling component the place where the actions below it show their sentences.
 *
 * @returns The last sentence an action below reported; null until one has.
 */
export const providePluginActionMessages = (): Ref<PluginActionMessage | null> => {
  const message = ref<PluginActionMessage | null>(null)
  provide(PLUGIN_ACTION_MESSAGE_KEY, (next: PluginActionMessage) => {
    message.value = next
  })
  return message
}

/**
 * Reads the outlet an action reports its sentence to.
 *
 * @returns The outlet, or null when the action stands alone and shows its sentence itself.
 */
export const usePluginActionMessageOutlet = (): PluginActionMessageOutlet | null => {
  return inject(PLUGIN_ACTION_MESSAGE_KEY, null)
}
