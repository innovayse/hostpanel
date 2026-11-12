/**
 * Tests for `usePluginChatTurn`: the history read when mounted, a send that keeps the stored draft until the first word,
 * a send that got no reply (refused, stopped before the first word, to an unusable route) giving its text back, leaving
 * the page keeping the stored copy, a restart that interrupts a send forgetting it, the blocks read again after a turn,
 * a send that carried an element selection staying shown when it was stopped before the first word, and the reply being
 * written. The admin has no selection of its own, so the hooks here are a stand-in for the shared code path.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { usePluginChatTurn } from '@/composables/usePluginChatTurn'
import type { PluginChatSelectionHooks, PluginChatTransport, PluginChatTurn } from '@/composables/usePluginChatTurn'
import { providePluginView } from '@/composables/usePluginView'
import type { PluginViewContext } from '@/composables/usePluginView'
import type { ServerSentEvent as PluginStreamEvent } from '@/types/http'
import { PluginBlockKind, PluginChatRole } from '@/types/pluginview'
import type { PluginCallResult, PluginChatBlock } from '@/types/pluginview'

afterEach(() => {
  window.sessionStorage.clear()
})

/** A stream the fake transport holds open until the test ends it. */
interface OpenStream {
  /** The body the message was sent with. */
  body: Record<string, unknown>
  /** Hands an event to the chat. */
  onEvent: (event: PluginStreamEvent) => void
  /** The signal that aborts the stream. */
  signal: AbortSignal
  /** Ends the stream with an outcome. */
  finish: (result: PluginCallResult) => void
}

/**
 * An outcome of a call.
 *
 * @param over - What differs from a success.
 * @returns The outcome.
 */
const outcome = (over: Partial<PluginCallResult> = {}): PluginCallResult => ({
  ok: true, data: null, error: '', fieldErrors: {}, unavailable: false, ...over,
})

/**
 * Builds a transport whose streams stay open until the test finishes them.
 *
 * @param history - What the history route answers.
 * @returns The transport and the streams it opened.
 */
const fakeTransport = (history: PluginCallResult = outcome({ data: [] })): { transport: PluginChatTransport, streams: OpenStream[] } => {
  const streams: OpenStream[] = []
  const transport: PluginChatTransport = {
    callRoute: vi.fn(async () => history),
    streamRoute: vi.fn((_pluginId, _route, _values, body, onEvent, signal) =>
      new Promise<PluginCallResult>((resolve) => {
        streams.push({ body, onEvent, signal, finish: resolve })
      })),
  }
  return { transport, streams }
}

/**
 * The drafts kept in the tab.
 *
 * @returns The stored texts.
 */
const stored = (): string[] =>
  Object.keys(window.sessionStorage)
    .filter(key => key.startsWith('hostpanel.plugin-chat-draft'))
    .map(key => window.sessionStorage.getItem(key) ?? '')

/**
 * A delta event.
 *
 * @param text - The words.
 * @returns The event.
 */
const delta = (text: string): PluginStreamEvent => ({ type: 'delta', data: { text } })

/**
 * Mounts a chat over a fake transport.
 *
 * @param transport - The transport.
 * @param over - What differs in the block.
 * @param selection - The element selection hooks, if the chat has any.
 * @returns The turn, the wrapper, the view context and the focus calls.
 */
const mountTurn = (transport: PluginChatTransport, over: Partial<PluginChatBlock> = {}, selection?: PluginChatSelectionHooks) => {
  const block: PluginChatBlock = { kind: PluginBlockKind.Chat, send: { method: 'post', path: '/chat' }, ...over }
  const context = {
    pluginId: 'acme', values: { siteId: '7' }, externalOrigins: [], frameOrigins: [], refreshes: ref<Record<string, number>>({}),
    refresh: vi.fn(),
  } as unknown as PluginViewContext
  const focus = vi.fn()
  let turn!: PluginChatTurn
  const Child = defineComponent({
    setup() {
      turn = usePluginChatTurn({ block: () => block, store: transport, focusField: focus, selection })
      return () => h('div')
    },
  })
  const wrapper = mount(defineComponent({
    setup() {
      providePluginView(context)
      return () => h(Child)
    },
  }))
  return { turn, wrapper, context, focus }
}

/**
 * Types a message into the field and sends it.
 *
 * @param turn - The chat.
 * @param text - The message.
 * @returns The send, which resolves when the reply has ended.
 */
const typeAndSend = (turn: PluginChatTurn, text: string): Promise<void> => {
  turn.draft.value = text
  return turn.send()
}

describe('usePluginChatTurn', () => {
  it('reads the history when mounted, and shows the sentence of a refused read', async () => {
    const read = fakeTransport(outcome({ data: [{ role: 'user', text: 'Earlier' }] }))
    const { turn } = mountTurn(read.transport, { history: { method: 'get', path: '/history' } })

    expect(turn.loaded.value).toBe(false)
    await flushPromises()
    expect(turn.loaded.value).toBe(true)
    expect(turn.messages.value.map(message => message.text)).toEqual(['Earlier'])

    const refused = fakeTransport(outcome({ ok: false, error: 'No history.' }))
    const second = mountTurn(refused.transport, { history: { method: 'get', path: '/history' } })
    await flushPromises()
    expect(second.turn.error.value).toBe('No history.')
    expect(second.turn.loaded.value).toBe(true)
  })

  it('sends the trimmed draft, keeps the stored copy until the first word, then forgets it', async () => {
    const { transport, streams } = fakeTransport()
    const { turn, focus } = mountTurn(transport)

    const sending = typeAndSend(turn, '  Add a page  ')

    expect(streams[0]!.body).toEqual({ message: 'Add a page' })
    expect(focus).toHaveBeenCalledTimes(1)
    expect(turn.draft.value).toBe('')
    expect(turn.streaming.value).toBe(true)
    expect(stored()).toEqual(['  Add a page  '])
    streams[0]!.onEvent(delta('On it'))
    expect(stored()).toEqual([])
    streams[0]!.finish(outcome())
    await sending

    expect(turn.streaming.value).toBe(false)
    expect(turn.messages.value.map(message => message.text)).toEqual(['Add a page', 'On it'])
    expect(turn.retryable.value).toBe(false)
  })

  it('sends nothing for an empty draft or while a reply streams', async () => {
    const { transport, streams } = fakeTransport()
    const { turn } = mountTurn(transport)

    await turn.send()
    expect(streams).toHaveLength(0)

    const sending = typeAndSend(turn, 'One')
    turn.draft.value = 'Two'
    await turn.send()
    await turn.pick('Three')
    expect(streams).toHaveLength(1)
    streams[0]!.finish(outcome())
    await sending
  })

  it('follows which reply is being written', async () => {
    const { transport, streams } = fakeTransport()
    const { turn } = mountTurn(transport)

    const sending = typeAndSend(turn, 'Hi')
    const reply = turn.messages.value[1]!

    expect(turn.isWriting(reply.key)).toBe(true)
    expect(turn.isWriting(turn.messages.value[0]!.key)).toBe(false)
    streams[0]!.finish(outcome())
    await sending
    expect(turn.isWriting(reply.key)).toBe(false)
  })

  it('gives the text back and shows the sentence when the send is refused before any reply', async () => {
    const { transport, streams } = fakeTransport()
    const { turn } = mountTurn(transport)

    const sending = typeAndSend(turn, 'Add a page')
    streams[0]!.finish(outcome({ ok: false, error: 'Too many requests.' }))
    await sending

    expect(turn.draft.value).toBe('Add a page')
    expect(turn.messages.value).toEqual([])
    expect(turn.error.value).toBe('Too many requests.')
    expect(turn.retryable.value).toBe(false)
    expect(stored()).toEqual(['Add a page'])
  })

  it('gives the text back, with no sentence, when the route is one the page cannot call', async () => {
    const { transport, streams } = fakeTransport()
    const { turn, context } = mountTurn(transport, { refresh: ['feed'] })

    const sending = typeAndSend(turn, 'Add a page')
    streams[0]!.finish(outcome({ unavailable: true }))
    await sending

    expect(turn.draft.value).toBe('Add a page')
    expect(turn.messages.value).toEqual([])
    expect(turn.error.value).toBe('')
    expect(turn.retryable.value).toBe(false)
    expect(stored()).toEqual(['Add a page'])
    expect(context.refresh).not.toHaveBeenCalled()
  })

  it('gives the text back and keeps the stored copy when Stop comes before the first word', async () => {
    const { transport, streams } = fakeTransport()
    const { turn } = mountTurn(transport)

    const sending = typeAndSend(turn, 'Add a page')
    turn.stop()
    expect(streams[0]!.signal.aborted).toBe(true)
    streams[0]!.finish(outcome())
    await sending

    expect(turn.draft.value).toBe('Add a page')
    expect(turn.messages.value).toEqual([])
    expect(turn.error.value).toBe('')
    expect(stored()).toEqual(['Add a page'])
  })

  it('keeps what arrived when Stop comes after the first words, and forgets the draft', async () => {
    const { transport, streams } = fakeTransport()
    const { turn } = mountTurn(transport)

    const sending = typeAndSend(turn, 'Add a page')
    streams[0]!.onEvent(delta('Half'))
    turn.stop()
    streams[0]!.finish(outcome())
    await sending

    expect(turn.messages.value.map(message => message.text)).toEqual(['Add a page', 'Half'])
    expect(turn.draft.value).toBe('')
    expect(stored()).toEqual([])
    expect(turn.error.value).toBe('')
  })

  it('keeps the stored copy when the page is left while the send is out', async () => {
    const { transport, streams } = fakeTransport()
    const { turn, wrapper } = mountTurn(transport)

    const sending = typeAndSend(turn, 'Add a page')
    wrapper.unmount()
    expect(streams[0]!.signal.aborted).toBe(true)
    streams[0]!.finish(outcome())
    await sending

    expect(stored()).toEqual(['Add a page'])
  })

  it('forgets the stored copy when a send that carried a selection is stopped before the first word', async () => {
    const { transport, streams } = fakeTransport()
    const hooks: PluginChatSelectionHooks = {
      spend: () => ({ body: { selection: { releaseId: 'r1' } }, mark: vi.fn() }),
      repeat: () => undefined,
    }
    const { turn } = mountTurn(transport, {}, hooks)

    const sending = typeAndSend(turn, 'Make it blue')
    turn.stop()
    streams[0]!.finish(outcome())
    await sending

    expect(turn.messages.value.map(message => message.role)).toEqual([PluginChatRole.User])
    expect(turn.draft.value).toBe('')
    expect(stored()).toEqual([])
  })

  it('keeps the stored copy when the page is left during a send that carried a selection', async () => {
    const { transport, streams } = fakeTransport()
    const hooks: PluginChatSelectionHooks = {
      spend: () => ({ body: { selection: { releaseId: 'r1' } }, mark: vi.fn() }),
      repeat: () => undefined,
    }
    const { turn, wrapper } = mountTurn(transport, {}, hooks)

    const sending = typeAndSend(turn, 'Make it blue')
    wrapper.unmount()
    streams[0]!.finish(outcome())
    await sending

    expect(stored()).toEqual(['Make it blue'])
  })

  it('forgets the stored copy of a send that a restart interrupted, and clears the screen', async () => {
    const { transport, streams } = fakeTransport()
    const { turn, context } = mountTurn(transport, { id: 'conversation' })

    const sending = typeAndSend(turn, 'Add a page')
    context.refreshes.value = { conversation: 1 }
    await nextTick()
    expect(streams[0]!.signal.aborted).toBe(true)
    expect(turn.messages.value).toEqual([])
    streams[0]!.finish(outcome())
    await sending

    expect(stored()).toEqual([])
    expect(turn.draft.value).toBe('')
    expect(turn.streaming.value).toBe(false)
  })

  it('offers Retry for a failed turn that wrote a reply, and replaces that turn', async () => {
    const { transport, streams } = fakeTransport()
    const { turn } = mountTurn(transport)

    const sending = typeAndSend(turn, 'Add a page')
    streams[0]!.onEvent(delta('Part'))
    streams[0]!.finish(outcome({ ok: false, error: 'Lost.' }))
    await sending
    expect(turn.retryable.value).toBe(true)

    const again = turn.resendLast(true)
    expect(streams[1]!.body).toEqual({ message: 'Add a page' })
    expect(turn.messages.value.map(message => message.role)).toEqual([PluginChatRole.User, PluginChatRole.Assistant])
    expect(turn.error.value).toBe('')
    streams[1]!.finish(outcome())
    await again
  })

  it('reads the blocks the chat names again once a turn ended', async () => {
    const { transport, streams } = fakeTransport()
    const { turn, context } = mountTurn(transport, { refresh: ['feed'] })

    const sending = typeAndSend(turn, 'Add a page')
    streams[0]!.onEvent(delta('Done'))
    streams[0]!.finish(outcome())
    await sending

    expect(context.refresh).toHaveBeenCalledWith(['feed'])
  })
})
