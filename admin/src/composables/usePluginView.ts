/**
 * The wiring of a rendered plugin view: the view hands its context (plugin, page values, link origins,
 * refresh signals, follow-ups) to the blocks below it, and a block reads its source route through the
 * plugin ui store. A block's `visibleWhen` condition and an action's or form's `followUp` are read the
 * same way. Reads of one route are shared inside a view (see {@link readPluginRoute}).
 *
 * The context is rendering wiring, not business data, so it travels by provide/inject; every call still
 * goes through `stores/pluginUi.ts`.
 */

import { computed, inject, onMounted, provide, ref, shallowRef, useId, watch } from 'vue'
import type { ComputedRef, InjectionKey, Ref } from 'vue'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS, PLUGIN_FOLLOW_UP_MAX_ATTEMPTS, PLUGIN_VIEW_READ_MAX_AGE_MS, pluginConditionHolds, pluginRouteMethod, pluginRouteRequest, readPluginField } from '@/lib/pluginView'
import { usePluginUiStore } from '@/stores/pluginUi'
import { HttpMethod } from '@/types/http'
import type { PluginCallResult, PluginFollowUp, PluginPageLink, PluginSource, PluginViewCondition, PluginViewRoute } from '@/types/pluginview'

/** The context of one rendered plugin view. */
export interface PluginViewContext {
  /** The plugin id. */
  pluginId: string
  /** The page parameter values by name, read from the address. */
  values: Record<string, string>
  /** The plugin's allowed link origins. */
  externalOrigins: string[]
  /** A counter per block id, bumped to make that block read its source again. */
  refreshes: Ref<Record<string, number>>
  /** Bumps the counters of the named blocks. */
  refresh: (ids: readonly string[]) => void
  /** Opens another page of the plugin, its parameters read from a row or reply. */
  open: (link: PluginPageLink, row: unknown) => void
  /**
   * Puts a running follow-up in the view's keeping: the view ends it when it is left, not before, so a
   * block that hides itself while its own work runs cannot end the watch it started.
   *
   * @param key - The starting block's id.
   * @param stop - Ends the watch.
   * @returns Lets go of the watch once it ended on its own.
   */
  adopt: (key: string, stop: () => void) => () => void
}

/** The injection key of the view context. */
const PLUGIN_VIEW_KEY: InjectionKey<PluginViewContext> = Symbol('plugin-view')

/** The injection key that tells a block it sits in a split pane. */
const PLUGIN_PANE_KEY: InjectionKey<boolean> = Symbol('plugin-pane')

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

/**
 * Tells the blocks below the calling component that they sit in a split pane.
 *
 * @returns Nothing.
 */
export const providePluginPane = (): void => {
  provide(PLUGIN_PANE_KEY, true)
}

/**
 * Whether the calling block sits in a split pane.
 *
 * @returns True inside a pane.
 */
export const usePluginPane = (): boolean => inject(PLUGIN_PANE_KEY, false)

/** The reads a view has made since it last changed, by request address. */
interface PluginViewReads {
  /** The sum of the view's refresh counts when these reads were made. */
  refreshed: number
  /** The store's count of writes when these reads were made. */
  written: number
  /** When the first of these reads was made, in milliseconds since the epoch. */
  since: number
  /** The answer of each request, or the read still out for it. */
  reads: Map<string, Promise<PluginCallResult>>
}

/** The reads of each view, held by its context so they go with the view. */
const VIEW_READS = new WeakMap<PluginViewContext, PluginViewReads>()

/**
 * The reads a view's blocks share right now: empty again whenever the view was refreshed, a write
 * started or ended, or the picture grew older than {@link PLUGIN_VIEW_READ_MAX_AGE_MS}.
 *
 * @param context - The view's context.
 * @param store - The plugin ui store, which counts the writes.
 * @returns The shared reads of the view in its present state.
 */
const sharedReads = (context: PluginViewContext, store: ReturnType<typeof usePluginUiStore>): PluginViewReads => {
  const refreshed = Object.values(context.refreshes.value).reduce((sum, count) => sum + count, 0)
  const written = store.writes
  const now = Date.now()
  let current = VIEW_READS.get(context)
  if (current === undefined || current.refreshed !== refreshed || current.written !== written || now - current.since >= PLUGIN_VIEW_READ_MAX_AGE_MS) {
    current = { refreshed, written, since: now, reads: new Map() }
    VIEW_READS.set(context, current)
  }
  return current
}

/**
 * Reads a GET route for a block, a condition or a card, sharing the read with every other reader of the
 * same request in the view's present state, so a header and the blocks beside it never disagree. A
 * failed read is not kept: the next reader tries again.
 *
 * @param context - The view's context.
 * @param store - The plugin ui store.
 * @param route - The route.
 * @param query - Query parameters; another query is another request.
 * @returns The outcome of the read; never rejects.
 */
const readPluginRoute = (
  context: PluginViewContext,
  store: ReturnType<typeof usePluginUiStore>,
  route: PluginViewRoute,
  query: Record<string, string> = {},
): Promise<PluginCallResult> => {
  const request = pluginRouteRequest(route, context.values, query)
  if (request === null || request.method !== HttpMethod.Get) {
    return store.callRoute(context.pluginId, route, context.values, undefined, query)
  }
  const shared = sharedReads(context, store)
  const held = shared.reads.get(request.url)
  if (held !== undefined) {
    return held
  }
  const read = store.callRoute(context.pluginId, route, context.values, undefined, query)
  shared.reads.set(request.url, read)
  void read.then((result) => {
    if (!result.ok && shared.reads.get(request.url) === read) {
      shared.reads.delete(request.url)
    }
  })
  return read
}

/**
 * Hands the view an answer it already holds, so the readers that ask next take it instead of asking again.
 *
 * @param context - The view's context.
 * @param store - The plugin ui store.
 * @param route - The route that was read.
 * @param result - What the read answered; a failure is not shared.
 * @returns Nothing.
 */
const shareReadResult = (
  context: PluginViewContext,
  store: ReturnType<typeof usePluginUiStore>,
  route: PluginViewRoute,
  result: PluginCallResult,
): void => {
  const request = pluginRouteRequest(route, context.values)
  if (request !== null && request.method === HttpMethod.Get && result.ok) {
    sharedReads(context, store).reads.set(request.url, Promise.resolve(result))
  }
}

/** What {@link usePluginSource} answers. */
export interface PluginSourceState {
  /** The value read: the reply, or its `field`; undefined until read or after a failure. */
  value: Ref<unknown>
  /** True while reading. */
  loading: Ref<boolean>
  /** The sentence of a failed read, the API's; empty when there is none. */
  error: Ref<string>
  /** The source names a route the page cannot read: render nothing. */
  unavailable: Ref<boolean>
  /** Reads again. */
  reload: () => Promise<void>
}

/**
 * Reads a block's source route when the block mounts, again whenever an action asks the block to
 * refresh, and again when the query changes. A source naming a route that is not a GET reads nothing.
 *
 * @param source - The block's source; nothing is read while it is undefined.
 * @param blockId - The block's id, which actions name to refresh it.
 * @param query - Query parameters sent with the read.
 * @returns The value, loading flag, error sentence, unavailable flag and a reload action.
 */
export const usePluginSource = (
  source: () => PluginSource | undefined,
  blockId: () => string | undefined,
  query: () => Record<string, string> = () => ({}),
): PluginSourceState => {
  const context = usePluginView()
  const store = usePluginUiStore()

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
    const result = await readPluginRoute(context, store, current.route, query())
    if (request !== latest) {
      return
    }
    loading.value = false
    unavailable.value = result.unavailable
    error.value = result.error
    value.value = result.ok ? readPluginField(result.data, current.field) : undefined
  }

  onMounted(() => {
    void reload()
  })
  watch(() => {
    const id = blockId()
    return id ? context.refreshes.value[id] ?? 0 : 0
  }, reload)
  watch(query, reload, { deep: true })

  return { value, loading, error, unavailable, reload }
}

/**
 * Whether a block, or a card action, shows by its `visibleWhen` condition: read when it mounts and again
 * whenever one of the named block ids is refreshed. Hidden while the condition is read, on a failed read
 * and when it does not hold; shown only once it is known to hold. No condition always shows.
 *
 * @param condition - The condition; undefined for none.
 * @param refreshIds - The block ids whose refresh reads the condition again.
 * @returns Whether the block shows.
 */
export const usePluginCondition = (
  condition: () => PluginViewCondition | undefined,
  refreshIds: () => readonly string[],
): ComputedRef<boolean> => {
  const context = usePluginView()
  const store = usePluginUiStore()

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
    if (pluginRouteMethod(current.route) !== HttpMethod.Get) {
      holds.value = false
      return
    }
    const result = await readPluginRoute(context, store, current.route)
    if (request !== latest) {
      return
    }
    holds.value = result.ok && pluginConditionHolds(current, result.data)
  }

  onMounted(() => {
    if (condition()) {
      void reload()
    }
  })
  // The counters only rise, so their sum changes exactly when one of the named blocks was refreshed.
  watch(() => refreshIds().reduce((sum, id) => sum + (context.refreshes.value[id] ?? 0), 0), reload)

  return computed(() => !condition() || holds.value)
}

/**
 * Runs an action's or a form's `followUp` after its call succeeded: every
 * {@link PLUGIN_FOLLOW_UP_INTERVAL_MS} it reads the condition's route and asks the named blocks to read
 * again, and it stops once the condition holds, after {@link PLUGIN_FOLLOW_UP_MAX_ATTEMPTS} reads, or when
 * the view is left. The interval and the number of attempts are the host's, never the plugin's. Starting
 * again cancels a follow-up still running.
 *
 * @param owner - The id of the calling block; undefined for a block with no id.
 * @returns Starts a follow-up; the second argument is told when the condition held.
 */
export const usePluginFollowUp = (
  owner: () => string | undefined = () => undefined,
): ((followUp: PluginFollowUp | undefined, settled?: () => void) => void) => {
  const context = usePluginView()
  const store = usePluginUiStore()
  /** The key a block without an id keeps its one watch under. */
  const instanceKey = `follow-up-${useId()}`

  /** The pending wait before the next attempt. */
  let timer: ReturnType<typeof setTimeout> | undefined
  /** The number of the current follow-up; a read for an older one is ignored. */
  let run = 0
  /** Lets the view go of the running watch once it has ended. */
  let release: (() => void) | undefined

  /**
   * Ends the running follow-up, if any.
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
   * Lets the view go of the running watch, which has ended on its own.
   *
   * @returns Nothing.
   */
  const letGo = (): void => {
    release?.()
    release = undefined
  }

  /**
   * One attempt: reads the condition, refreshes the named blocks and waits for the next attempt unless it is over.
   *
   * @param current - The follow-up this attempt belongs to.
   * @param followUp - What to watch and refresh.
   * @param attempt - The attempt's number, from 1.
   * @param settled - Told when the condition held.
   * @returns Resolves when the attempt has ended.
   */
  const attemptOnce = async (current: number, followUp: PluginFollowUp, attempt: number, settled: (() => void) | undefined): Promise<void> => {
    timer = undefined
    const result = await store.callRoute(context.pluginId, followUp.until.route, context.values)
    if (current !== run) {
      return
    }
    if (result.unavailable) {
      letGo()
      return
    }
    const done = result.ok && pluginConditionHolds(followUp.until, result.data)
    // Refreshed after the read, so the blocks show exactly what the condition saw.
    context.refresh([...followUp.refresh])
    shareReadResult(context, store, followUp.until.route, result)
    if (done || attempt >= PLUGIN_FOLLOW_UP_MAX_ATTEMPTS) {
      letGo()
      if (done) {
        settled?.()
      }
      return
    }
    timer = setTimeout(() => {
      void attemptOnce(current, followUp, attempt + 1, settled)
    }, PLUGIN_FOLLOW_UP_INTERVAL_MS)
  }

  return (followUp: PluginFollowUp | undefined, settled?: () => void): void => {
    stop()
    letGo()
    if (!followUp || pluginRouteMethod(followUp.until.route) !== HttpMethod.Get) {
      return
    }
    release = context.adopt(owner() ?? instanceKey, stop)
    const current = run
    timer = setTimeout(() => {
      void attemptOnce(current, followUp, 1, settled)
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

/** Where an action reports its sentence when a header shows one line for its whole row of actions. */
export type PluginActionMessageOutlet = (message: PluginActionMessage) => () => void

/** The injection key of the action message outlet. */
const PLUGIN_ACTION_MESSAGE_KEY: InjectionKey<PluginActionMessageOutlet> = Symbol('plugin-action-message')

/**
 * Makes the calling component the place where the actions below it show their sentences.
 *
 * @returns The last sentence an action below reported; null until one has.
 */
export const providePluginActionMessages = (): Ref<PluginActionMessage | null> => {
  /** The last sentence; shallow, so the take-back below compares it by identity. */
  const message = shallowRef<PluginActionMessage | null>(null)
  provide(PLUGIN_ACTION_MESSAGE_KEY, (next: PluginActionMessage) => {
    message.value = next
    return () => {
      // Only the line this call put up is taken down: a newer sentence from another action stays.
      if (message.value === next) {
        message.value = null
      }
    }
  })
  return message
}

/**
 * Reads the outlet an action reports its sentence to.
 *
 * @returns The outlet, or null when the action stands alone and shows its sentence itself.
 */
export const usePluginActionMessageOutlet = (): PluginActionMessageOutlet | null => inject(PLUGIN_ACTION_MESSAGE_KEY, null)
