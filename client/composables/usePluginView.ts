/**
 * The wiring of a rendered plugin view: the view hands its context (page values, origins, refresh
 * signals) to the blocks below it, and a block reads its source route through the plugin store. A
 * block's `visibleWhen` condition and an action's or form's `followUp` are read the same way.
 *
 * Reads of one route are shared inside a view: see {@link readPluginRoute}.
 *
 * The context is rendering wiring, not business data, so it travels by provide/inject; every call
 * still goes through `stores/plugins.ts`.
 *
 * @module composables/usePluginView
 */

import type { ComputedRef, InjectionKey, Ref } from 'vue'
import { PLUGIN_FOLLOW_UP_INTERVAL_MS, PLUGIN_FOLLOW_UP_MAX_ATTEMPTS } from '~/constants/pluginFollowUp'
import { PLUGIN_VIEW_READ_MAX_AGE_MS } from '~/constants/pluginViewReads'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginSource } from '~/types/pluginblock'
import type { PluginFollowUp } from '~/types/pluginfollowup'
import { HttpMethod } from '~/types/httpmethod'
import type { PluginViewCondition } from '~/types/pluginviewcondition'
import type { PluginViewContext } from '~/types/pluginviewcontext'
import type { PluginViewRoute } from '~/types/pluginviewroute'
import type { PluginCallResult } from '~/types/plugincallresult'
import { pluginConditionHolds, pluginRouteMethod, pluginRouteRequest, readPluginField } from '~/utils/plugins'

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

/** The injection key that tells a block it sits in a split pane, which gives it its height on a wide screen. */
const PLUGIN_PANE_KEY: InjectionKey<boolean> = Symbol('plugin-pane')

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

/** The link back from a workspace page. */
export interface PluginBackLink {
  /** Where it goes. */
  to: string
  /** Its text. */
  label: string
}

/** The injection key of the page's back link. */
const PLUGIN_BACK_LINK_KEY: InjectionKey<ComputedRef<PluginBackLink | null>> = Symbol('plugin-back-link')

/**
 * Offers the page's back link to a compact header below, which draws it on its own line.
 *
 * @param link - The link; null while the page has none.
 * @returns Nothing.
 */
export const providePluginBackLink = (link: ComputedRef<PluginBackLink | null>): void => {
  provide(PLUGIN_BACK_LINK_KEY, link)
}

/**
 * Reads the page's back link.
 *
 * @returns The link, or null outside a page that offers one.
 */
export const usePluginBackLink = (): PluginBackLink | null => inject(PLUGIN_BACK_LINK_KEY, null)?.value ?? null

/**
 * The reads a view has made since it last changed, by request. A view is one picture of the plugin's data: every block
 * and every card that asks for the same route in it is shown the same answer, which is also what keeps a header and the
 * blocks beside it from disagreeing.
 */
interface PluginViewReads {
  /** The sum of the view's refresh counts when these reads were made. */
  refreshed: number
  /** The store's count of writes (starts and ends) when these reads were made. */
  written: number
  /** The moment the first of these reads was made, in milliseconds since the epoch; the picture is not kept past its age limit. */
  since: number
  /** The answer of each request, or the read still out for it, by the request's address. */
  reads: Map<string, Promise<PluginCallResult>>
}

/** The reads of each view, held by its context so they go with the view and never reach another one. */
const VIEW_READS = new WeakMap<PluginViewContext, PluginViewReads>()

/**
 * The reads a view's blocks share right now. They start empty again whenever the view was refreshed, a call that may
 * change the plugin's data started or ended, or the picture grew older than {@link PLUGIN_VIEW_READ_MAX_AGE_MS}: those
 * are the moments the view knows its picture may be out of date.
 *
 * @param context - The view's context.
 * @param store - The plugin store, which counts the writes.
 * @returns The shared reads of the view in its present state.
 */
const sharedReads = (context: PluginViewContext, store: ReturnType<typeof usePluginsStore>): PluginViewReads => {
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
 * Reads a GET route for a block, a condition or a card, sharing the read with every other reader of the same request
 * in the view's present state.
 *
 * A page with a header, badges, a button and a conversation names one state route many times, and each of them reads it
 * when it mounts and again when it is refreshed. Left alone that is one request per reader per refresh, which on a route
 * the plugin answers one call at a time makes the last readers' answers arrive after the next refresh began, so they are
 * dropped as older and the blocks that read first show the new value long before the rest. So the first reader of a
 * request sends it, the others take its answer, and the next refresh (or the end of a write) starts a new one. A read
 * that failed is not kept: the next reader tries again.
 *
 * @param context - The view's context, which owns the reads.
 * @param store - The plugin store the read goes through.
 * @param route - The route, a GET the page can fill; anything else is called as it is, unshared.
 * @param query - Query parameters, e.g. a table's page; another query is another request.
 * @returns The outcome of the read; never rejects.
 */
const readPluginRoute = (
  context: PluginViewContext,
  store: ReturnType<typeof usePluginsStore>,
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
 * Hands the view an answer it already holds as the answer of its present state, so the readers that ask next take it
 * instead of sending the same request again. A follow-up uses it for the read that told it the work had moved: the
 * blocks it then refreshes show exactly what that read saw.
 *
 * @param context - The view's context.
 * @param store - The plugin store.
 * @param route - The route that was read.
 * @param result - What the read answered; a failure is not shared.
 * @returns Nothing.
 */
const shareReadResult = (
  context: PluginViewContext,
  store: ReturnType<typeof usePluginsStore>,
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
    const result = await readPluginRoute(context, store, current.route, query())
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
 * Whether a block, or a part of one, shows by its `visibleWhen` condition: read when it mounts and again whenever an
 * action asks any of the named block ids to refresh, so a follow-up can reveal or hide it. A block names its own id; a
 * result card's action has no id of its own and names the blocks its chat reads again after a turn, since those are the
 * ones whose data the condition is about.
 *
 * A block with no condition always shows and reads nothing. A block with one is hidden while the condition is being
 * read, when the read fails or is unavailable, and when the field is absent or matches none of the values: it shows
 * only once the condition is known to hold, so a create form never flashes up for a site that already exists.
 *
 * @param condition - The condition; undefined for none.
 * @param refreshIds - The block ids whose refresh reads the condition again; none means it is read once, on mounting.
 * @returns Whether the block shows.
 */
export const usePluginCondition = (
  condition: () => PluginViewCondition | undefined,
  refreshIds: () => readonly string[],
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
    const result = await readPluginRoute(context, store, current.route)
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
  // The counters only ever rise, so their sum changes exactly when one of the named blocks was refreshed.
  watch(() => refreshIds().reduce((sum, id) => sum + (context.refreshes.value[id] ?? 0), 0), reload)

  return computed(() => !condition() || holds.value)
}

/**
 * Runs an action's or a form's `followUp` after its call succeeded: every {@link PLUGIN_FOLLOW_UP_INTERVAL_MS} it
 * reads the condition's route and asks the named blocks to read again, and it stops once the condition holds (after
 * that last refresh), after {@link PLUGIN_FOLLOW_UP_MAX_ATTEMPTS} reads, or when the view is left, which is also what
 * leaving the page does. A failed read counts as an attempt and the watch goes on.
 *
 * The watch belongs to the view, not to the block that started it. A block can hide itself in the middle of its own
 * follow-up (a publish button whose `visibleWhen` stops holding once the version it publishes is live), and the
 * refresh that hides it is read a moment before the attempt that would have shown the final value in the other blocks;
 * a watch that ended with its block left the header on the old version for good.
 *
 * The interval and the number of attempts are the host's: a view names what to watch and never how often, so no
 * plugin can make the panel poll faster or longer. Starting again cancels a follow-up still running, so two clicks
 * never run two watches.
 *
 * @param owner - The id of the calling block, which a later watch of the same block replaces; undefined for a block
 *   with no id, whose watches are told apart by the block instance.
 * @returns Starts a follow-up (undefined starts nothing); the second argument is told when the condition held, which
 *   is the end of the work the call started.
 */
export const usePluginFollowUp = (
  owner: () => string | undefined = () => undefined,
): ((followUp: PluginFollowUp | undefined, settled?: () => void) => void) => {
  const context = usePluginView()
  const store = usePluginsStore()
  // One block instance without an id still owns one watch at a time.
  const instanceKey = `follow-up-${useId()}`

  /** The pending wait before the next attempt; undefined while none is pending. */
  let timer: ReturnType<typeof setTimeout> | undefined
  /** The number of the current follow-up; a read answering for an older one, or after the view is left, is ignored. */
  let run = 0
  /** Lets the view go of the running watch once it has ended; undefined while none is adopted. */
  let release: (() => void) | undefined

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
  const attemptOnce = async (
    current: number,
    followUp: PluginFollowUp,
    attempt: number,
    settled: (() => void) | undefined,
  ): Promise<void> => {
    timer = undefined
    const result = await store.callRoute(context.pluginId, followUp.until.route, context.values)
    // An answer for an older follow-up, or one after the view was left, is not this run's: leave the view's keeping
    // alone.
    if (current !== run) {
      return
    }
    // A route the page cannot read ends the watch without a refresh; the view must not keep its stop function.
    if (result.unavailable) {
      letGo()
      return
    }
    const done = result.ok && pluginConditionHolds(followUp.until, result.data)
    // Refreshed after the read, so the final value the condition saw is also what the blocks show: the blocks that read
    // the same route take this very answer instead of asking again.
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
    // A condition on a route that is not a GET is refused at install and could never end the watch: not started.
    if (!followUp || pluginRouteMethod(followUp.until.route) !== HttpMethod.Get) {
      return
    }
    // Adopting ends an earlier watch of the same block, even one a block instance that has since gone started.
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

/**
 * Where an action reports its sentence when something above it shows the sentences of a row of actions: the header
 * draws one line under its whole action row, because a sentence drawn under one button grows that button's column
 * and pulls the row out of line. It answers a function that takes the sentence back, which does nothing once another
 * sentence has replaced it.
 */
export type PluginActionMessageOutlet = (message: PluginActionMessage) => () => void

/** The injection key of the action message outlet. */
const PLUGIN_ACTION_MESSAGE_KEY: InjectionKey<PluginActionMessageOutlet> = Symbol('plugin-action-message')

/**
 * Makes the calling component the place where the actions below it show their sentences.
 *
 * @returns The last sentence an action below reported; null until one has.
 */
export const providePluginActionMessages = (): Ref<PluginActionMessage | null> => {
  /** The last sentence an action below reported; null until one has. */
  // Shallow: takeBack compares the line by identity, which a deep ref's proxy would never equal.
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
export const usePluginActionMessageOutlet = (): PluginActionMessageOutlet | null => {
  return inject(PLUGIN_ACTION_MESSAGE_KEY, null)
}
