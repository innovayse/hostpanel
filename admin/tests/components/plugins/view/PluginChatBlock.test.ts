/**
 * Unit tests for PluginChatBlock: the history, the intro and suggestions, a streamed reply with its
 * steps, result card, issue and links under the link rule, a refused stream, and the refresh after a turn.
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
})

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
 * Types a message and sends it.
 *
 * @param wrapper - The mounted view.
 * @param text - The message.
 * @returns Resolves once the reply has been read.
 */
const sendMessage = async (wrapper: Awaited<ReturnType<typeof mountPluginView>>['wrapper'], text: string): Promise<void> => {
  await wrapper.get('[data-testid="plugin-chat-input"]').setValue(text)
  await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
  await flushPromises()
  await flushPromises()
}

describe('PluginChatBlock', () => {
  it('reads the history and offers the intro and suggestions only while empty', async () => {
    const empty = await mountPluginView([chat({ intro: { title: 'Hello', text: 'Ask anything.' }, suggestions: [{ label: 'Status', text: 'What is the status?' }] })])
    expect(empty.wrapper.get('[data-testid="plugin-chat-intro-title"]').text()).toBe('Hello')
    await empty.wrapper.get('[data-testid="plugin-chat-suggestion-0"]').trigger('click')
    expect((empty.wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('What is the status?')
    expect(empty.wrapper.get('[data-testid="plugin-chat-input"]').attributes('placeholder')).toBe('Ask the assistant')

    const { wrapper } = await mountPluginView([chat({ history: { method: 'get', path: '/chat' }, intro: { title: 'Hello' } })], {
      routes: { [`GET ${ext('chat')}`]: () => json([{ role: 'user', text: 'Hi' }, { role: 'assistant', text: 'Hello **there**' }]) },
    })
    expect(wrapper.find('[data-testid="plugin-chat-intro"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-chat-user"]').text()).toBe('Hi')
    expect(wrapper.get('[data-testid="plugin-chat-assistant"] strong').text()).toBe('there')
    expect(wrapper.get('[data-testid="plugin-chat-assistant-label"]').text()).toBe('Assistant')
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

  it('shows the API sentence of a refused stream and leaves no empty reply', async () => {
    const { wrapper } = await mountPluginView([chat()], {
      routes: { [`POST ${ext('chat')}`]: () => problem('Out of credits.', 429) },
    })

    await sendMessage(wrapper, 'Hello')

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('Out of credits.')
    expect(wrapper.find('[data-testid="plugin-chat-assistant"]').exists()).toBe(false)
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
})
