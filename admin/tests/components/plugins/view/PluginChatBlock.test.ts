/**
 * Unit tests for PluginChatBlock: the history, the intro and suggestions, a streamed reply with its
 * steps, result card, issue and links under the link rule, a refused stream, and the refresh after a turn; the
 * chat layout (Enter and Shift+Enter, typing dots, caret, status inside the reply, copy, time, Jump to latest),
 * Stop, leaving the page, a restart by id, the unsent draft kept in the tab, and Retry after a failed turn.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { PluginBlockKind } from '@/types/pluginview'
import type { PluginChatBlock } from '@/types/pluginview'
import { json, problem } from '../../../helpers/fetch'
import { requestsOf } from '../../../helpers/routes'
import { eventStream, ext, mountPluginView } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
  // A kept draft belongs to the tab; the next test must start with a tab that has none.
  window.sessionStorage.clear()
})

/** The mounted view, as the helper hands it over. */
type Mounted = Awaited<ReturnType<typeof mountPluginView>>

/**
 * Builds the chat under test.
 *
 * @param overrides - Members to change.
 * @returns The chat block.
 */
const chat = (overrides: Partial<PluginChatBlock> = {}): PluginChatBlock => ({
  kind: PluginBlockKind.Chat,
  send: { method: 'post', path: '/chat' },
  placeholder: 'Ask the assistant',
  assistant: { label: 'Assistant' },
  ...overrides,
})

/**
 * Types a message and sends it with the button.
 *
 * @param wrapper - The mounted view.
 * @param text - The message.
 * @returns Resolves once the reply has been read.
 */
const sendMessage = async (wrapper: Mounted['wrapper'], text: string): Promise<void> => {
  await wrapper.get('[data-testid="plugin-chat-input"]').setValue(text)
  await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
  await flushPromises()
  await flushPromises()
}

/** A reply the test keeps open and writes into by hand. */
interface OpenStream {
  /** The response the stubbed route answers with. */
  response: Response
  /** Writes one event. */
  push: (event: string, data: unknown) => void
  /** Whether the reader cancelled the stream, as Stop or leaving the page does. */
  cancelled: () => boolean
}

/**
 * Opens a stream that stays open until the test closes it or the chat cancels it.
 *
 * @returns The response and what writes into it.
 */
const openStream = (): OpenStream => {
  const encoder = new TextEncoder()
  let writer: ReadableStreamDefaultController<Uint8Array> | null = null
  let cancelled = false
  const body = new ReadableStream<Uint8Array>({
    start: (controller) => {
      writer = controller
    },
    cancel: () => {
      cancelled = true
    },
  })
  return {
    response: new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } }),
    push: (event, data) => writer?.enqueue(encoder.encode(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`)),
    cancelled: () => cancelled,
  }
}

/**
 * The drafts kept in the tab.
 *
 * @returns The stored texts.
 */
const stored = (): string[] =>
  Object.keys(window.sessionStorage)
    .filter((key) => key.startsWith('hostpanel.plugin-chat-draft'))
    .map((key) => window.sessionStorage.getItem(key) ?? '')

describe('PluginChatBlock', () => {
  it('reads the history and offers the intro and suggestions only while empty', async () => {
    const sent: unknown[] = []
    const empty = await mountPluginView([chat({ intro: { title: 'Hello', text: 'Ask anything.' }, suggestions: [{ label: 'Status', text: 'What is the status?' }] })], {
      routes: { [`POST ${ext('chat')}`]: (body) => {
        sent.push(body)
        return eventStream([{ event: 'message', data: { text: 'Fine.' } }])
      } },
    })
    expect(empty.wrapper.get('[data-testid="plugin-chat-intro-title"]').text()).toBe('Hello')
    expect(empty.wrapper.get('[data-testid="plugin-chat-input"]').attributes('placeholder')).toBe('Ask the assistant')

    await empty.wrapper.get('[data-testid="plugin-chat-suggestion-0"]').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(sent).toEqual([{ message: 'What is the status?' }])
    expect(empty.wrapper.find('[data-testid="plugin-chat-empty"]').exists()).toBe(false)

    const { wrapper } = await mountPluginView([chat({ history: { method: 'get', path: '/chat' }, intro: { title: 'Hello' } })], {
      routes: { [`GET ${ext('chat')}`]: () => json([{ role: 'user', text: 'Hi' }, { role: 'assistant', text: 'Hello **there**' }]) },
    })
    expect(wrapper.find('[data-testid="plugin-chat-empty"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-chat-user"]').text()).toBe('Hi')
    expect(wrapper.get('[data-testid="plugin-chat-assistant"] strong').text()).toBe('there')
    expect(wrapper.get('[data-testid="plugin-chat-assistant-label"]').text()).toBe('Assistant')
    expect(wrapper.find('[data-testid="plugin-chat-time"]').exists()).toBe(false)
  })

  it('uses the block\'s placeholder as the sentence of an empty chat without an introduction', async () => {
    const { wrapper } = await mountPluginView([chat()])

    expect(wrapper.get('[data-testid="plugin-chat-intro-text"]').text()).toBe('Ask the assistant')
  })

  it('shows the API\'s sentence when the history is refused', async () => {
    const { wrapper } = await mountPluginView([chat({ history: { method: 'get', path: '/chat' } })], {
      routes: { [`GET ${ext('chat')}`]: () => problem('The plugin failed.', 502) },
    })

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('The plugin failed.')
  })

  it('streams a reply with its steps, card, issue and only the links the rule admits', async () => {
    let sent: unknown = null
    const { wrapper } = await mountPluginView([chat()], {
      externalOrigins: ['https://docs.example.com'],
      routes: {
        [`POST ${ext('chat')}`]: (body) => {
          sent = body
          return eventStream([
            { event: 'step', data: { id: 'a', label: 'Reading', state: 'running' } },
            { event: 'delta', data: { text: 'Done, see [here](https://evil.test).' } },
            { event: 'step', data: { id: 'a', label: 'Reading', state: 'done' } },
            { event: 'link', data: { text: 'Guide', url: 'https://docs.example.com/guide' } },
            { event: 'link', data: { text: 'Bad', url: 'https://evil.test/' } },
            { event: 'issue', data: { text: 'One image was skipped.' } },
            { event: 'result', data: { tone: 'success', title: 'Published', text: 'Version 2 is live.', details: ['line one'] } },
            { event: 'done', data: {} },
          ])
        },
      },
    })

    await sendMessage(wrapper, 'Publish it')

    expect(sent).toEqual({ message: 'Publish it' })
    expect(wrapper.get('[data-testid="plugin-chat-user"]').text()).toBe('Publish it')
    const reply = wrapper.get('[data-testid="plugin-chat-assistant"]')
    expect(reply.text()).toContain('Done, see here.')
    expect(reply.find('a[href="https://evil.test"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="plugin-chat-link"]').map((link) => link.attributes('href'))).toEqual(['https://docs.example.com/guide'])
    expect(wrapper.get('[data-testid="plugin-chat-step"]').attributes('data-state')).toBe('done')
    expect(wrapper.get('[data-testid="plugin-chat-steps-count"]').text()).toBe('1 of 1 done')
    expect(wrapper.get('[data-testid="plugin-chat-issue"]').text()).toContain('One image was skipped.')
    expect(wrapper.get('[data-testid="plugin-chat-result-title"]').text()).toBe('Published')

    await wrapper.get('[data-testid="plugin-chat-result-details-toggle"]').trigger('click')
    expect(wrapper.get('[data-testid="plugin-chat-result-details"]').text()).toBe('line one')
  })

  it('shows the API sentence of a refused stream, leaves no empty reply and gives the message back to the field', async () => {
    const { wrapper } = await mountPluginView([chat()], {
      routes: { [`POST ${ext('chat')}`]: () => problem('Out of credits.', 429) },
    })

    await sendMessage(wrapper, 'Hello')

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('Out of credits.')
    expect(wrapper.find('[data-testid="plugin-chat-assistant"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-user"]').exists()).toBe(false)
    expect((wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('Hello')
    expect(wrapper.find('[data-testid="plugin-chat-retry"]').exists()).toBe(false)
  })

  it('sends nothing for an empty message', async () => {
    const { wrapper, fetch } = await mountPluginView([chat()])

    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('   ')

    expect(wrapper.get('[data-testid="plugin-chat-send"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="plugin-chat-input"]').trigger('keydown', { key: 'Enter' })
    expect(requestsOf(fetch).filter((line) => line.startsWith('POST'))).toEqual([])
  })

  it('reads the named blocks again once a reply has ended', async () => {
    const { wrapper, fetch } = await mountPluginView([
      { kind: PluginBlockKind.Stat, id: 'credits', label: 'Credits', source: { route: { method: 'get', path: '/credits' }, field: 'left' } },
      chat({ refresh: ['credits'] }),
    ], {
      routes: {
        [`GET ${ext('credits')}`]: () => json({ left: 5 }),
        [`POST ${ext('chat')}`]: () => eventStream([{ event: 'message', data: { text: 'Hi.' } }]),
      },
    })

    await sendMessage(wrapper, 'Hello')

    expect(requestsOf(fetch).filter((line) => line === `GET ${ext('credits')}`)).toHaveLength(2)
  })

  it('sends on Enter and writes a new line on Shift+Enter', async () => {
    const { wrapper, fetch } = await mountPluginView([chat()], {
      routes: { [`POST ${ext('chat')}`]: () => eventStream([{ event: 'message', data: { text: 'Hi.' } }]) },
    })
    const field = wrapper.get('[data-testid="plugin-chat-input"]')
    await field.setValue('Hello')

    await field.trigger('keydown', { key: 'Enter', shiftKey: true })
    expect(requestsOf(fetch).filter((line) => line.startsWith('POST'))).toHaveLength(0)

    await field.trigger('keydown', { key: 'Enter' })
    await flushPromises()
    await flushPromises()
    expect(requestsOf(fetch).filter((line) => line.startsWith('POST'))).toHaveLength(1)
    expect((field.element as HTMLTextAreaElement).value).toBe('')
  })

  it('shows the typing dots until the first text, then the text with a caret that ends with the reply', async () => {
    const open = openStream()
    const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })
    await sendMessage(wrapper, 'Build')
    expect(wrapper.find('[data-testid="ui-typing-dots"]').exists()).toBe(true)

    open.push('delta', { text: 'Working' })
    await flushPromises()
    expect(wrapper.find('[data-testid="ui-typing-dots"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-assistant"] [data-testid="markdown-caret"]').exists()).toBe(true)

    open.push('done', {})
    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="markdown-caret"]').exists()).toBe(false)
  })

  it('shows what the plugin is doing inside the reply being written, until the reply ends', async () => {
    const open = openStream()
    const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })
    await sendMessage(wrapper, 'Build')

    open.push('status', { text: 'Building pages…' })
    await flushPromises()
    expect(wrapper.get('[data-testid="plugin-chat-reply"] [data-testid="plugin-chat-status"]').text()).toBe('Building pages…')

    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="plugin-chat-status"]').exists()).toBe(false)
  })

  it('replaces Send with Stop while a reply runs, and on Stop marks a running step stopped with no failure', async () => {
    const open = openStream()
    const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })
    await sendMessage(wrapper, 'Add a page')
    open.push('delta', { text: 'Half' })
    open.push('step', { id: 'a', label: 'Building', state: 'running' })
    await flushPromises()
    expect(wrapper.find('[data-testid="plugin-chat-send"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(open.cancelled()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-chat-assistant"]').text()).toBe('Half')
    expect(wrapper.get('[data-testid="plugin-chat-step"]').attributes('data-state')).toBe('stopped')
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-send"]').exists()).toBe(true)
  })

  it('closes the stream when the page is left', async () => {
    const open = openStream()
    const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })
    await sendMessage(wrapper, 'Add a page')

    wrapper.unmount()
    await flushPromises()

    expect(open.cancelled()).toBe(true)
  })

  it('starts over when another block refreshes it by id: the reply stops, the screen clears, the history is read again', async () => {
    const open = openStream()
    let historyReads = 0
    const { wrapper } = await mountPluginView([
      chat({ id: 'conversation', history: { method: 'get', path: '/chat' } }),
      { kind: PluginBlockKind.Action, label: 'New conversation', route: { method: 'post', path: '/reset' }, refresh: ['conversation'] },
    ], {
      routes: {
        [`GET ${ext('chat')}`]: () => json(++historyReads === 1 ? [{ role: 'user', text: 'Old question' }] : []),
        [`POST ${ext('chat')}`]: () => open.response,
        [`POST ${ext('reset')}`]: () => new Response(null, { status: 204 }),
      },
    })
    await sendMessage(wrapper, 'Add a page')
    open.push('delta', { text: 'Half' })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(open.cancelled()).toBe(true)
    expect(historyReads).toBe(2)
    expect(wrapper.findAll('[data-testid="plugin-chat-user"]')).toHaveLength(0)
    expect(wrapper.findAll('[data-testid="plugin-chat-assistant"]')).toHaveLength(0)
    expect(wrapper.find('[data-testid="plugin-chat-stop"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
  })

  it('labels the log for assistive technology and marks it busy while a reply streams', async () => {
    const open = openStream()
    const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })
    const log = wrapper.get('[data-testid="plugin-chat-log"]')

    expect(log.attributes('role')).toBe('log')
    // Focusable, so a keyboard reader can scroll it.
    expect(log.attributes('tabindex')).toBe('0')
    expect(log.attributes('aria-label')).toBe('Conversation')
    expect(log.attributes('aria-busy')).toBe('false')

    await sendMessage(wrapper, 'Build')
    expect(log.attributes('aria-busy')).toBe('true')

    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await flushPromises()
  })

  it('shows the time of messages written in this session only', async () => {
    const { wrapper } = await mountPluginView([chat({ history: { method: 'get', path: '/chat' } })], {
      routes: {
        [`GET ${ext('chat')}`]: () => json([{ role: 'user', text: 'Earlier' }, { role: 'assistant', text: 'Before' }]),
        [`POST ${ext('chat')}`]: () => eventStream([{ event: 'message', data: { text: 'Now.' } }]),
      },
    })
    expect(wrapper.findAll('[data-testid="plugin-chat-time"]')).toHaveLength(0)

    await sendMessage(wrapper, 'Hi')

    expect(wrapper.findAll('[data-testid="plugin-chat-time"]')).toHaveLength(2)
  })

  it('copies a finished reply', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const { wrapper } = await mountPluginView([chat()], {
      routes: { [`POST ${ext('chat')}`]: () => eventStream([{ event: 'message', data: { text: 'The **answer**' } }]) },
    })
    await sendMessage(wrapper, 'Hi')

    await wrapper.get('[data-testid="plugin-chat-copy"]').trigger('click')

    expect(writeText).toHaveBeenCalledWith('The **answer**')
  })

  it('offers a way back to the newest message once the operator scrolled up', async () => {
    const open = openStream()
    const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })
    await sendMessage(wrapper, 'Build')
    const log = wrapper.get('[data-testid="plugin-chat-log"]')
    const element = log.element as HTMLElement
    Object.defineProperty(element, 'scrollHeight', { configurable: true, value: 1000 })
    Object.defineProperty(element, 'clientHeight', { configurable: true, value: 200 })
    expect(wrapper.find('[data-testid="plugin-chat-jump"]').exists()).toBe(false)

    element.scrollTop = 100
    await log.trigger('scroll')
    open.push('delta', { text: 'More' })
    await flushPromises()
    expect(element.scrollTop).toBe(100)

    await wrapper.get('[data-testid="plugin-chat-jump"]').trigger('click')
    await flushPromises()
    expect(element.scrollTop).toBe(1000)
    expect(wrapper.find('[data-testid="plugin-chat-jump"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await flushPromises()
  })

  it('shows the count only near the limit', async () => {
    const { wrapper } = await mountPluginView([chat({ maxLength: 10 })])
    const field = wrapper.get('[data-testid="plugin-chat-input"]')

    await field.setValue('abc')
    expect(wrapper.find('[data-testid="ui-chat-input-count"]').exists()).toBe(false)

    await field.setValue('abcdefghi')
    expect(wrapper.get('[data-testid="ui-chat-input-count"]').text()).toBe('9/10')
  })

  describe('the unsent draft', () => {
    it('is kept in the tab and restored when the chat is mounted again', async () => {
      const first = await mountPluginView([chat()])
      await first.wrapper.get('[data-testid="plugin-chat-input"]').setValue('Half a thought')
      expect(stored()).toEqual(['Half a thought'])
      first.wrapper.unmount()

      const second = await mountPluginView([chat()])

      expect((second.wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('Half a thought')
    })

    it('stays kept while a send is out and is forgotten once a reply arrived', async () => {
      const open = openStream()
      const { wrapper } = await mountPluginView([chat()], { routes: { [`POST ${ext('chat')}`]: () => open.response } })

      await sendMessage(wrapper, 'Add a page')
      expect(stored()).toEqual(['Add a page'])

      open.push('delta', { text: 'On it' })
      await flushPromises()
      expect(stored()).toEqual([])

      await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
      await flushPromises()
    })

    it('is kept apart for another page of the same plugin', async () => {
      const first = await mountPluginView([chat()])
      await first.wrapper.get('[data-testid="plugin-chat-input"]').setValue('For site 7')
      first.wrapper.unmount()

      const other = await mountPluginView([chat()], { values: { siteId: '8' } })

      expect((other.wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('')
    })
  })

  it('offers Retry after a failed turn that wrote a reply, and sends the same message in place of the failed turn', async () => {
    const bodies: unknown[] = []
    const { wrapper } = await mountPluginView([chat()], {
      routes: {
        [`POST ${ext('chat')}`]: (body) => {
          bodies.push(body)
          return bodies.length === 1
            ? eventStream([{ event: 'delta', data: { text: 'Part' } }, { event: 'error', data: { text: 'The model is busy.' } }, { event: 'done', data: {} }])
            : eventStream([{ event: 'message', data: { text: 'Fixed.' } }])
        },
      },
    })
    await sendMessage(wrapper, 'Add a page')
    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('The model is busy.')
    expect(wrapper.findAll('[data-testid="plugin-chat-user"]')).toHaveLength(1)

    await wrapper.get('[data-testid="plugin-chat-retry"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(bodies).toEqual([{ message: 'Add a page' }, { message: 'Add a page' }])
    expect(wrapper.findAll('[data-testid="plugin-chat-user"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugin-chat-assistant"]').text()).toBe('Fixed.')
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-retry"]').exists()).toBe(false)
  })
})
