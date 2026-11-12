/**
 * The turns of one plugin chat: sending a message and showing the reply as it streams, Stop, Retry, a card's "send again",
 * a restart when another block refreshes the chat, and reading the history. It joins the messages, the draft and the
 * log's scrolling (`usePluginChatMessages`, `usePluginChatDraft`, `usePluginChatScroll`) with the plugin store's transport,
 * and holds the screen state the chat block draws: the failure line, whether a reply streams, which reply is being written.
 *
 * The chat block only draws and wires the parts of its app: it names the store, the field's focus and, where the app has
 * one, the element selection (`selection`). Nothing else differs between the apps, so this file is the same in both.
 *
 * @module composables/usePluginChatTurn
 */

import type { ComputedRef, Ref } from 'vue'
import { usePluginChatDraft } from '~/composables/usePluginChatDraft'
import { usePluginChatMessages } from '~/composables/usePluginChatMessages'
import { usePluginChatScroll } from '~/composables/usePluginChatScroll'
import { usePluginView } from '~/composables/usePluginView'
import type { PluginCallResult } from '~/types/plugincallresult'
import type { PluginChatBlock } from '~/types/pluginblock'
import type { PluginChatMessage } from '~/types/pluginchatmessage'
import { PluginChatRole } from '~/types/pluginchatrole'
import { PluginChatStepState } from '~/types/pluginchatstepstate'
import type { PluginStreamEvent } from '~/types/pluginstreamevent'
import type { PluginViewRoute } from '~/types/pluginviewroute'

/** The part of the plugin store a chat calls: its history read and its stream. */
export interface PluginChatTransport {
  /**
   * Reads a GET route.
   *
   * @param pluginId - The plugin.
   * @param route - The route.
   * @param values - The values its `{param}` segments are filled from.
   * @returns The outcome; never throws.
   */
  callRoute: (pluginId: string, route: PluginViewRoute, values: Record<string, string>) => Promise<PluginCallResult>
  /**
   * Opens a streaming POST route and hands each event over as it arrives.
   *
   * @param pluginId - The plugin.
   * @param route - The route.
   * @param values - The values its `{param}` segments are filled from.
   * @param body - The JSON body.
   * @param onEvent - Called with each event, in order.
   * @param signal - Aborting it closes the stream, which then ends without a failure.
   * @returns The outcome; never throws.
   */
  streamRoute: (
    pluginId: string,
    route: PluginViewRoute,
    values: Record<string, string>,
    body: Record<string, unknown>,
    onEvent: (event: PluginStreamEvent) => void,
    signal: AbortSignal,
  ) => Promise<PluginCallResult>
}

/** What a message carries from the element selection, once the selection is spent on it. */
export interface PluginChatCarried {
  /** The members added to the message's body. */
  body: Record<string, unknown>
  /**
   * Marks the message the reader sees with what it carries, so a "try again" can send the same.
   *
   * @param message - The reader's message.
   * @returns Nothing.
   */
  mark: (message: PluginChatMessage) => void
}

/** The element selection of an app that has one; the chat sends and resends through it. */
export interface PluginChatSelectionHooks {
  /**
   * Spends the selection the chat holds on a new message.
   *
   * @returns What the message carries; undefined when the chat holds no selection.
   */
  spend: () => PluginChatCarried | undefined
  /**
   * Gives what a message sent again must carry.
   *
   * @param message - The message to send again.
   * @returns What it carries; undefined when it carried nothing; null when it must not go because the page changed since
   *   its elements were selected, which fails closed: a message with no selection reads as free to change anything.
   */
  repeat: (message: PluginChatMessage) => PluginChatCarried | undefined | null
}

/** What {@link usePluginChatTurn} is given. */
export interface PluginChatTurnOptions {
  /** The block, read when needed so a changed prop is followed. */
  block: () => PluginChatBlock
  /** The plugin store's transport. */
  store: PluginChatTransport
  /** Puts the keyboard focus back in the message field. */
  focusField: () => void
  /** The element selection; absent where the app has none. */
  selection?: PluginChatSelectionHooks
}

/** What {@link usePluginChatTurn} hands out. */
export interface PluginChatTurn {
  /** The messages shown, oldest first. */
  messages: Ref<PluginChatMessage[]>
  /** The message field's text. */
  draft: Ref<string>
  /** The log element, bound with `ref="log"`. */
  log: Ref<HTMLElement | null>
  /** Whether the log follows the newest message. */
  following: Ref<boolean>
  /** The sentence of a failure; empty when there is none. */
  error: Ref<string>
  /** Whether the failed turn can be sent again from the failure line. */
  retryable: Ref<boolean>
  /** True when a "try again" was refused because the page changed after its elements were selected. */
  selectionStale: Ref<boolean>
  /** True while a reply streams. */
  streaming: Ref<boolean>
  /** True once the history has been read (or there is none), so the empty state does not flash before it. */
  loaded: Ref<boolean>
  /** Whether a message can be sent now. */
  canSend: ComputedRef<boolean>
  /**
   * Whether a message is the reply being written, which ends in the caret.
   *
   * @param key - The message's key.
   * @returns True while that reply streams.
   */
  isWriting: (key: number) => boolean
  /**
   * Follows the log's own scrolling.
   *
   * @returns Nothing.
   */
  onScroll: () => void
  /**
   * Scrolls to the newest message and follows it again.
   *
   * @returns Nothing.
   */
  jumpToLatest: () => void
  /**
   * Sends the draft and empties the field; the field keeps the focus.
   *
   * @returns Resolves when the reply has ended, been stopped or failed.
   */
  send: () => Promise<void>
  /**
   * Sends a ready-made message of the empty state; what the reader has typed stays in the field.
   *
   * @param text - The message.
   * @returns Resolves when the reply has ended, been stopped or failed.
   */
  pick: (text: string) => Promise<void>
  /**
   * Sends the conversation's last message again.
   *
   * @param replace - True for a retry, which removes the failed turn first; false for a card's "try again", which adds to it.
   * @returns Resolves when the reply has ended.
   */
  resendLast: (replace: boolean) => Promise<void>
  /**
   * Stops the reply streaming now; what arrived stays.
   *
   * @returns Nothing.
   */
  stop: () => void
}

/**
 * Runs the turns of one chat. Call it from the chat block's setup: it reads the history when mounted, starts over when
 * another block refreshes the chat by its `id`, and closes the stream when the page is left.
 *
 * @param options - The block, the store's transport, the field's focus and the element selection, if the app has one.
 * @returns The state the block draws and the actions it wires.
 */
export const usePluginChatTurn = (options: PluginChatTurnOptions): PluginChatTurn => {
  const { block, store, focusField, selection } = options
  /** The view's context: the plugin and the route values. */
  const context = usePluginView()

  /** The sentence of a failure; empty when there is none. */
  const error = ref('')
  /** Whether the failed turn can be sent again from the failure line. */
  const retryable = ref(false)
  /** True when a "try again" was refused because the page changed after its elements were selected. */
  const selectionStale = ref(false)
  /** True while a reply streams. */
  const streaming = ref(false)
  /** True once the history has been read (or there is none), so the empty state does not flash before it. */
  const loaded = ref(false)
  /** The key of the reply being written; -1 when none. */
  const writingKey = ref(-1)
  /** Aborts the stream in flight, if any. */
  let controller: AbortController | null = null
  /** Counts the conversations shown; a history read or a reply from an earlier one is dropped when it lands. */
  let generation = 0
  /** True once the page was left, so a send that ends afterwards keeps its stored copy for the next visit. */
  let left = false

  /** The messages and what changes them. */
  const { messages, add, addHistory, apply, hasContent, dropFrom, remove, clear } = usePluginChatMessages(
    context,
    () => block().refresh ?? [],
    (sentence) => {
      error.value = sentence
    },
  )
  /** The unsent draft. */
  const { draft, take, forget, giveBack } = usePluginChatDraft(context.pluginId, block().send.path, context.values)
  /** The log's scrolling. */
  const { log, following, onScroll, jumpToLatest } = usePluginChatScroll(messages)

  /** Whether a message can be sent now. */
  const canSend = computed(() => !streaming.value && draft.value.trim() !== '')

  /**
   * Whether a message is the reply being written.
   *
   * @param key - The message's key.
   * @returns True while that reply streams.
   */
  const isWriting = (key: number): boolean => streaming.value && key === writingKey.value

  /**
   * Reads the earlier messages, when the view names a history route.
   *
   * @returns Resolves when the read has ended.
   */
  const loadHistory = async (): Promise<void> => {
    const asked = generation
    const history = block().history
    if (history) {
      const result = await store.callRoute(context.pluginId, history, context.values)
      // A refresh started a new conversation while this read was out: its answer belongs to the old one.
      if (asked !== generation) {
        return
      }
      if (result.ok) {
        addHistory(result.data)
      } else {
        error.value = result.error
      }
    }
    loaded.value = true
  }

  /**
   * Sends a message and shows the reply as it streams.
   *
   * @param text - The message, already trimmed.
   * @param carried - What the element selection adds to it, if anything.
   * @returns Resolves when the reply has ended, been stopped or failed.
   */
  const sendMessage = async (text: string, carried?: PluginChatCarried): Promise<void> => {
    const sent = add(PluginChatRole.User, text)
    carried?.mark(sent)
    const target = add(PluginChatRole.Assistant, '')
    const asked = generation
    const running = new AbortController()
    controller = running
    following.value = true
    streaming.value = true
    writingKey.value = target.key
    error.value = ''
    retryable.value = false
    selectionStale.value = false
    /** Whether a reply has arrived yet; the draft is forgotten at the first one. */
    let arrived = false

    const result = await store.streamRoute(
      context.pluginId, block().send, context.values, { message: text, ...carried?.body },
      (event) => {
        apply(target, event)
        if (!arrived && hasContent(target)) {
          arrived = true
          forget()
        }
      },
      running.signal,
    )

    // The conversation was reset while the reply streamed: the reset already stopped it and cleared the screen, and the
    // text of that turn must not come back from the stored copy.
    if (asked !== generation) {
      forget()
      return
    }
    streaming.value = false
    writingKey.value = -1
    target.status = ''
    controller = null
    /** Whether the reader stopped the reply, or left the page. */
    const aborted = running.signal.aborted
    // The reader stopped watching: what is still running is not known to have finished, and is not called a failure.
    if (aborted) {
      for (const step of target.steps) {
        if (step.state === PluginChatStepState.Running) {
          step.state = PluginChatStepState.Stopped
        }
      }
    }
    // A refusal or a broken connection shows the store's sentence; an unusable route reference (`unavailable`) has
    // no sentence of its own to show, and a stop or a finished stream is `ok`.
    if (!result.ok && !result.unavailable) {
      error.value = result.error
    }
    // A turn that ran may have changed what other blocks show (a new version, fewer credits), stopped or not.
    const refresh = block().refresh
    if (!result.unavailable && refresh?.length) {
      context.refresh(refresh)
    }
    const failed = error.value !== ''
    const empty = !hasContent(target)
    // A send that got no reply (failed, stopped before the first word, or to a route the page cannot call) goes back to
    // the field, and is not left shown as sent. One that carried a selection stays: the selection was spent with it, and
    // only a retry can check it again before sending.
    const gaveBack = empty && carried === undefined && (failed || aborted || result.unavailable)
    if (gaveBack) {
      remove(sent.key, target.key)
      giveBack(text)
    } else {
      // A reply that never wrote a word leaves no empty row behind.
      if (empty) {
        remove(target.key)
      }
      // Only leaving the page keeps the stored copy for the next visit: a Stop that carried a selection stays shown as
      // sent, so its text must not come back from the store on a reload.
      if (!left) {
        forget()
      }
    }
    retryable.value = failed && !gaveBack
  }

  /**
   * Sends the draft and empties the field; the field keeps the focus.
   *
   * @returns Resolves when the reply has ended, been stopped or failed.
   */
  const send = async (): Promise<void> => {
    if (!canSend.value) {
      return
    }
    const text = take()
    focusField()
    await sendMessage(text, selection?.spend())
  }

  /**
   * Sends a ready-made message of the empty state; what the reader has typed stays in the field.
   *
   * @param text - The message.
   * @returns Resolves when the reply has ended, been stopped or failed.
   */
  const pick = async (text: string): Promise<void> => {
    if (streaming.value) {
      return
    }
    focusField()
    await sendMessage(text, selection?.spend())
  }

  /**
   * Sends the conversation's last message again. It never passes through the field, so what the reader has started typing
   * there stays. Refused when the selection hooks say the page changed since its elements were selected: the message then
   * waits in an empty field for the reader to select again.
   *
   * @param replace - True for a retry, which removes the failed turn first; false for a card's "try again", which adds to it.
   * @returns Resolves when the reply has ended.
   */
  const resendLast = async (replace: boolean): Promise<void> => {
    const last = [...messages.value].reverse().find(message => message.role === PluginChatRole.User)
    if (!last || streaming.value) {
      return
    }
    const carried = selection?.repeat(last)
    if (carried === null) {
      selectionStale.value = true
      if (draft.value.trim() === '') {
        draft.value = last.text
      }
      return
    }
    if (replace) {
      dropFrom(last.key)
    }
    focusField()
    await sendMessage(last.text, carried)
  }

  /**
   * Stops the reply streaming now; what arrived stays.
   *
   * @returns Nothing.
   */
  const stop = (): void => {
    controller?.abort()
  }

  /**
   * Starts a new conversation on screen: stops the reply in flight, clears what is shown and reads the history again,
   * which the plugin answers for the conversation it now holds.
   *
   * @returns Resolves when the history has been read again.
   */
  const restart = async (): Promise<void> => {
    generation++
    stop()
    controller = null
    streaming.value = false
    writingKey.value = -1
    clear()
    loaded.value = false
    error.value = ''
    retryable.value = false
    selectionStale.value = false
    await loadHistory()
  }

  onMounted(loadHistory)

  // Another block (a reset action) names this block in its `refresh`: the conversation it showed is gone.
  watch(() => {
    const id = block().id
    return id ? context.refreshes.value[id] ?? 0 : 0
  }, restart)

  // Leaving the page closes the stream, so the plugin stops writing for nobody.
  onBeforeUnmount(() => {
    left = true
    stop()
  })

  return {
    messages, draft, log, following, error, retryable, selectionStale, streaming, loaded, canSend,
    isWriting, onScroll, jumpToLatest, send, pick, resendLast, stop,
  }
}
