/**
 * Tests for PluginChatBlock: the history as escaped text, a reply streamed event by event through the real store
 * and `apiStream`, keepalives ignored, the plugin's and the host's error events, Stop, leaving the page, a broken
 * connection and a refused stream; the introduction and ready-made messages of an empty chat, the reply's signature,
 * the length cap, the blocks read again after a turn, a restart when another block refreshes the chat by id, and
 * links in a reply under the link-block rule.
 *
 * @module tests/components/plugin/ChatBlock.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import type { StubServer } from '~/tests/helpers/stubServer'
import { mountBlocks, problem, ROUTES_URL, startPluginApi, STREAM_URL, stubStream, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { settle } from '~/tests/helpers/settle'

/** The stub API of the running test, closed after it. */
let api: StubServer | undefined

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.unstubAllGlobals()
})

/** The chat of the worked example. */
const chat = { kind: PluginBlockKind.Chat, send: TEST_ROUTES.chat, history: TEST_ROUTES.history, placeholder: 'Describe a change' } as const

/** The history the stub answers. */
const HISTORY = [{ role: 'user', text: 'Make it blue' }, { role: 'assistant', text: 'Done <b>blue</b>' }]

/**
 * Mounts the chat with its history read.
 *
 * @returns The mounted view.
 */
const mountChat = async (): Promise<ReturnType<typeof mountBlocks>> => {
  api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7/history`]: { body: HISTORY } })
  const wrapper = mountBlocks([chat])
  await settle()
  return wrapper
}

/**
 * Types a message and sends it.
 *
 * @param wrapper - The mounted view.
 * @param text - The message.
 * @returns Resolves once the stream was opened.
 */
const sendMessage = async (wrapper: ReturnType<typeof mountBlocks>, text: string): Promise<void> => {
  await wrapper.get('[data-testid="plugin-chat-input"]').setValue(text)
  await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
  await settle()
}

/**
 * The last assistant bubble's text.
 *
 * @param wrapper - The mounted view.
 * @returns Its text.
 */
const lastReply = (wrapper: ReturnType<typeof mountBlocks>): string =>
  wrapper.findAll('[data-testid="plugin-chat-assistant"]').at(-1)?.text() ?? ''

describe('PluginChatBlock', () => {
  it('shows the earlier messages as text', async () => {
    const wrapper = await mountChat()

    expect(wrapper.get('[data-testid="plugin-chat-user"]').text()).toBe('Make it blue')
    expect(lastReply(wrapper)).toBe('Done <b>blue</b>')
    expect(wrapper.find('[data-testid="plugin-chat-assistant"] b').exists()).toBe(false)
  })

  it('shows the API\'s sentence when the history is refused', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7/history`]: problem(502, 'The plugin failed.') })
    const wrapper = mountBlocks([chat])
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('The plugin failed.')
  })

  it('opens the stream with the CSRF and language headers and streams the reply as it arrives', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()

    await sendMessage(wrapper, 'Add a page')
    const call = reply.calls[0]!
    expect(call.url).toBe(`${STREAM_URL}/sites/7/chat`)
    expect(JSON.parse(String(call.init.body))).toEqual({ message: 'Add a page' })
    const headers = call.init.headers as Record<string, string>
    expect(headers['X-Requested-With']).toBe('XMLHttpRequest')
    expect(headers['X-Language']).toBe('en')

    reply.send('status', { text: 'Building pages…' })
    reply.keepalive()
    reply.send('delta', { text: 'Added <i>' })
    await settle()
    expect(wrapper.get('[data-testid="plugin-chat-status"]').text()).toBe('Building pages…')
    expect(lastReply(wrapper)).toBe('Added <i>')
    expect(wrapper.find('[data-testid="plugin-chat-assistant"] i').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-stop"]').exists()).toBe(true)

    reply.send('delta', { text: 'the page.' })
    reply.send('done', {})
    reply.close()
    await settle()
    expect(lastReply(wrapper)).toBe('Added <i>the page.')
    expect(wrapper.find('[data-testid="plugin-chat-status"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-stop"]').exists()).toBe(false)
  })

  it('reads a reply as Markdown, keeps HTML in it as text and shows the reader\'s own message as typed', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, '**Keep** this')

    reply.send('delta', { text: '**Done** <img src=x onerror=alert(1)>' })
    reply.send('done', {})
    reply.close()
    await settle()

    const answer = wrapper.findAll('[data-testid="plugin-chat-assistant"]').at(-1)!
    expect(answer.get('strong').text()).toBe('Done')
    expect(answer.text()).toContain('<img src=x onerror=alert(1)>')
    expect(answer.find('img').exists()).toBe(false)
    const own = wrapper.findAll('[data-testid="plugin-chat-user"]').at(-1)!
    expect(own.text()).toBe('**Keep** this')
    expect(own.find('strong').exists()).toBe(false)
  })

  it.each([
    ['the plugin\'s own error event', { text: 'The model is busy.' }],
    ['the host\'s error frame after a plugin throw', { code: 'PLUGIN_FAILED', message: 'The plugin failed.', partial: true }],
  ])('shows the sentence of %s', async (_name, data) => {
    const reply = stubStream()
    const wrapper = await mountChat()

    await sendMessage(wrapper, 'Add a page')
    reply.send('delta', { text: 'Part' })
    reply.send('error', data)
    reply.send('done', {})
    reply.close()
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('text' in data ? data.text : data.message)
    expect(lastReply(wrapper)).toBe('Part')
  })

  it('stops on Stop: the stream is cancelled, what arrived stays, no failure is shown', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Add a page')
    reply.send('delta', { text: 'Half' })
    await settle()

    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await settle()

    expect((reply.calls[0]!.init.signal as AbortSignal).aborted).toBe(true)
    expect(reply.cancelled()).toBe(true)
    expect(lastReply(wrapper)).toBe('Half')
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
  })

  it('starts over when another block refreshes it by id: the reply stops, the screen clears, the history is read again', async () => {
    const reply = stubStream()
    let historyReads = 0
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7/history`]: () => ({ body: ++historyReads === 1 ? HISTORY : [] }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: { status: 204 },
    })
    const wrapper = mountBlocks([
      { ...chat, id: 'conversation' },
      { kind: PluginBlockKind.Action, label: 'New conversation', route: TEST_ROUTES.publish, refresh: ['conversation'] },
    ])
    await settle()
    await sendMessage(wrapper, 'Add a page')
    reply.send('delta', { text: 'Half' })
    await settle()

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect((reply.calls[0]!.init.signal as AbortSignal).aborted).toBe(true)
    expect(historyReads).toBe(2)
    expect(wrapper.findAll('[data-testid="plugin-chat-user"]')).toHaveLength(0)
    expect(wrapper.findAll('[data-testid="plugin-chat-assistant"]')).toHaveLength(0)
    expect(wrapper.find('[data-testid="plugin-chat-stop"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
  })

  it('closes the stream when the page is left', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Add a page')

    wrapper.unmount()
    await settle()

    expect((reply.calls[0]!.init.signal as AbortSignal).aborted).toBe(true)
  })

  it('shows the host\'s no-answer sentence when the connection breaks, keeping what arrived', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Add a page')
    reply.send('delta', { text: 'Part' })
    await settle()

    reply.fail()
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('errors.noAnswer')
    expect(lastReply(wrapper)).toBe('Part')
  })

  it('shows a refused stream\'s sentence and leaves no empty reply', async () => {
    stubStream({ status: 429, body: { type: 'RATE_LIMITED', title: 'Too Many Requests', status: 429, detail: 'Too many requests.' } })
    const wrapper = await mountChat()

    await sendMessage(wrapper, 'Add a page')

    expect(wrapper.get('[data-testid="plugin-chat-error"]').text()).toBe('Too many requests.')
    expect(wrapper.text()).not.toContain('Too Many Requests')
    expect(wrapper.text()).not.toContain('[object Object]')
    expect(wrapper.findAll('[data-testid="plugin-chat-assistant"]')).toHaveLength(1)
  })

  it('sends nothing for an empty message', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()

    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('   ')

    expect(wrapper.get('[data-testid="plugin-chat-send"]').attributes('disabled')).toBeDefined()
    expect(reply.calls).toHaveLength(0)
  })
  it('shows the introduction and ready-made messages of an empty chat, which only fill the field', async () => {
    const reply = stubStream()
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] } })
    const wrapper = mountBlocks([{
      ...chat,
      intro: { title: 'What should change?', text: 'Say it <b>plainly</b>.' },
      suggestions: [{ label: 'Gallery', text: 'Add a photo gallery.' }],
    }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-intro-title"]').text()).toBe('What should change?')
    expect(wrapper.get('[data-testid="plugin-chat-intro-text"]').text()).toBe('Say it <b>plainly</b>.')
    expect(wrapper.find('[data-testid="plugin-chat-intro"] b').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-chat-suggestion-0"]').trigger('click')

    expect((wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('Add a photo gallery.')
    expect(reply.calls).toHaveLength(0)
  })

  it('hides the introduction once the chat has messages', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7/history`]: { body: HISTORY } })
    const wrapper = mountBlocks([{ ...chat, intro: { title: 'What should change?' } }])
    await settle()

    expect(wrapper.find('[data-testid="plugin-chat-intro"]').exists()).toBe(false)
  })

  it('signs each reply with the assistant\'s name and caps the field at the block\'s length', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7/history`]: { body: HISTORY } })
    const wrapper = mountBlocks([{ ...chat, assistant: { label: 'Site assistant' }, maxLength: 80 }])
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-assistant-label"]').text()).toBe('Site assistant')
    expect(wrapper.get('[data-testid="plugin-chat-user"]').find('[data-testid="plugin-chat-assistant-label"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-chat-input"]').attributes('maxlength')).toBe('80')
  })

  it('reads the blocks the chat names again once a reply has ended', async () => {
    const reply = stubStream()
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'live' } },
    })
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Stat, id: 'status', label: 'Status', source: { route: TEST_ROUTES.site, field: 'status' } },
      { kind: PluginBlockKind.Chat, send: TEST_ROUTES.chat, refresh: ['status'] },
    ])
    await settle()
    /** How many times the site route has been read so far. */
    const reads = (): number => api!.requests.filter(request => request.url === `${ROUTES_URL}/sites/7`).length
    expect(reads()).toBe(1)

    await sendMessage(wrapper, 'Publish it')
    expect(reads()).toBe(1)
    reply.send('delta', { text: 'Done.' })
    reply.send('done', {})
    reply.close()
    await settle()

    expect(reads()).toBe(2)
  })
  it('adds a reply\'s link only when the link rule admits it, as text, opening an allowed origin in a new tab', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Show me')

    reply.send('delta', { text: 'Ready.' })
    reply.send('link', { text: 'Open <b>preview</b>', url: 'https://blog.sites.example.test/' })
    reply.send('link', { text: 'Your site', url: '/client/services/4' })
    reply.send('link', { text: 'Elsewhere', url: 'https://evil.example.test/' })
    reply.send('link', { text: 'Script', url: 'javascript:alert(1)' })
    reply.send('link', { text: 'Protocol relative', url: '//evil.example.test/' })
    reply.send('link', { text: 'Host API', url: '/client/../api/x' })
    reply.send('done', {})
    reply.close()
    await settle()

    const links = wrapper.findAll('[data-testid="plugin-chat-link"]')
    expect(links.map(link => link.text())).toEqual(['Open <b>preview</b>', 'Your site'])
    expect(links[0]!.find('b').exists()).toBe(false)
    expect(links[0]!.attributes('href')).toBe('https://blog.sites.example.test/')
    expect(links[0]!.attributes('target')).toBe('_blank')
    expect(links[0]!.attributes('rel')).toContain('noopener')
    expect(links[1]!.attributes('href')).toBe('/client/services/4')
  })

  it('keeps a reply that is only a link', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Show me')

    reply.send('link', { text: 'Open preview', url: 'https://blog.sites.example.test/' })
    reply.send('done', {})
    reply.close()
    await settle()

    expect(lastReply(wrapper)).toBe('Open preview')
  })
  it('shows the links a history entry kept, under the same rule as live ones', async () => {
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7/history`]: {
        body: [{
          role: 'assistant',
          text: 'Done.',
          links: [{ text: 'Preview', url: 'https://blog.sites.example.test/' }, { text: 'Evil', url: 'https://evil.example.test/' }],
        }],
      },
    })
    const wrapper = mountBlocks([chat])
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-chat-link"]').map(link => link.text())).toEqual(['Preview'])
  })

  it('keeps the latest 200 messages of a long history', async () => {
    const history = Array.from({ length: 250 }, (_, index) => ({ role: 'assistant', text: `m${index}` }))
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7/history`]: { body: history } })
    const wrapper = mountBlocks([chat])
    await settle()

    const shown = wrapper.findAll('[data-testid="plugin-chat-assistant"]')
    expect(shown).toHaveLength(200)
    expect(shown[0]!.text()).toBe('m50')
  })

  it('keeps one compact list of steps in the reply, a repeated id updating its row, and ignores a malformed step', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Build')

    reply.send('step', { id: 'build', label: 'Building', state: 'running' })
    reply.send('step', { label: 'Checks', state: 'failed' })
    reply.send('step', { id: 'build', label: 'Built', state: 'done' })
    reply.send('step', { label: 'Bad state', state: 'stopped' })
    reply.send('step', { state: 'done' })
    await settle()

    const rows = wrapper.findAll('[data-testid="plugin-chat-step"]')
    expect(rows.map(row => [row.text(), row.attributes('data-state')])).toEqual([['Built', 'done'], ['Checks', 'failed']])
  })

  it('draws a result card with its tone and checked actions, as text', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Publish')

    reply.send('result', {
      tone: 'success',
      title: '<b>Published</b>',
      text: 'Live.',
      actions: [
        { label: 'Open', href: 'https://x.sites.example.test/' },
        { label: 'Foreign', href: 'https://evil.test/' },
        { label: 'Roll back', route: TEST_ROUTES.publish },
        { label: 'Read', route: TEST_ROUTES.site },
        { label: 'Unfilled', route: { method: 'post', path: '/other/{missing}' } },
        { label: 'Sixth', href: 'https://y.sites.example.test/' },
      ],
    })
    reply.send('result', { tone: 'nonsense', text: 'Plain' })
    reply.send('result', { tone: 'danger' })
    reply.send('result', { title: 5, text: 'Wrong type' })
    await settle()

    const cards = wrapper.findAll('[data-testid="plugin-chat-result"]')
    expect(cards).toHaveLength(2)
    expect(cards[0]!.attributes('data-tone')).toBe('success')
    expect(cards[1]!.attributes('data-tone')).toBe('info')
    expect(cards[0]!.get('[data-testid="plugin-chat-result-title"]').text()).toBe('<b>Published</b>')
    expect(cards[0]!.find('b').exists()).toBe(false)
    expect(cards[0]!.findAll('[data-testid="plugin-chat-result-link"]').map(link => link.text())).toEqual(['Open'])
    expect(cards[0]!.findAll('[data-testid="plugin-action-button"]').map(button => button.text())).toEqual(['Roll back'])
  })

  it('calls a card\'s route through the plugin door', async () => {
    const reply = stubStream()
    let calls = 0
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`POST ${ROUTES_URL}/sites/7/publish`]: () => {
        calls += 1
        return { status: 204 }
      },
    })
    const wrapper = mountBlocks([chat])
    await settle()
    await sendMessage(wrapper, 'Publish')
    reply.send('result', { title: 'Ready', actions: [{ label: 'Publish', route: TEST_ROUTES.publish }] })
    await settle()

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(calls).toBe(1)
  })

  it('shows the plugin\'s own failure as an issue while the stream goes on, apart from the host\'s error', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Build')

    reply.send('issue', { text: 'The theme is missing.' })
    reply.send('issue', { text: 7 })
    reply.send('delta', { text: 'Still here' })
    await settle()

    expect(wrapper.findAll('[data-testid="plugin-chat-issue"]').map(issue => issue.text())).toEqual(['The theme is missing.'])
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
    expect(lastReply(wrapper)).toBe('Still here')
    expect(wrapper.find('[data-testid="plugin-chat-stop"]').exists()).toBe(true)
  })

  it('replaces Send with Stop while a reply runs, and on Stop marks a running step stopped with no failure', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Build')
    reply.send('step', { id: 'a', label: 'Done one', state: 'done' })
    reply.send('step', { id: 'b', label: 'Running one', state: 'running' })
    await settle()
    expect(wrapper.find('[data-testid="plugin-chat-send"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-chat-stop"]').trigger('click')
    await settle()

    expect((reply.calls[0]!.init.signal as AbortSignal).aborted).toBe(true)
    const rows = wrapper.findAll('[data-testid="plugin-chat-step"]')
    expect(rows.map(row => row.attributes('data-state'))).toEqual(['done', 'stopped'])
    expect(wrapper.find('[data-testid="plugin-chat-step-stopped"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-chat-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-send"]').exists()).toBe(true)
  })

  it('follows the reply in its own scrolling log, unless the reader scrolled up', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Build')
    const log = wrapper.get('[data-testid="plugin-chat-log"]')
    const element = log.element as HTMLElement
    Object.defineProperty(element, 'scrollHeight', { configurable: true, value: 1000 })
    Object.defineProperty(element, 'clientHeight', { configurable: true, value: 200 })

    reply.send('delta', { text: 'One' })
    await settle()
    expect(element.scrollTop).toBe(1000)

    element.scrollTop = 100
    await log.trigger('scroll')
    reply.send('delta', { text: 'Two' })
    await settle()
    expect(element.scrollTop).toBe(100)
  })

  it('reads the chat\'s blocks again after a card\'s action, as after a reply', async () => {
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: { body: { status: 'Before' } },
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: {} },
    })
    const reply = stubStream()
    const wrapper = mountBlocks([
      { kind: PluginBlockKind.Stat, id: 'status', label: 'Status', source: { route: TEST_ROUTES.site, field: 'status' } },
      { kind: PluginBlockKind.Chat, send: TEST_ROUTES.chat, refresh: ['status'] },
    ])
    await settle()
    await sendMessage(wrapper, 'Go')
    reply.send('result', { title: 'Ready', actions: [{ label: 'Undo', route: TEST_ROUTES.publish }] })
    await settle()
    const reads = (): number => api?.requests.filter(request => request.method === 'GET' && request.url.endsWith('/sites/7')).length ?? 0
    const before = reads()

    await wrapper.get('[data-testid="plugin-chat-result"] [data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(reads()).toBe(before + 1)
  })

  it('counts a reply\'s finished steps', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Go')

    reply.send('step', { id: 'a', label: 'Read', state: 'done' })
    reply.send('step', { id: 'b', label: 'Build', state: 'running' })
    await settle()

    expect(wrapper.find('[data-testid="plugin-chat-steps-count"]').exists()).toBe(true)
  })

  it('keeps a card\'s details behind a switch, as text, and sends the last message again on a resend action', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Add a page')
    reply.send('result', { title: 'Failed', details: ['tests <b>failed</b>'], actions: [{ label: 'Try again', resend: true }] })
    reply.close()
    await settle()

    expect(wrapper.find('[data-testid="plugin-chat-result-details"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-chat-result-details-toggle"]').trigger('click')
    expect(wrapper.get('[data-testid="plugin-chat-result-details"]').text()).toBe('tests <b>failed</b>')

    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    await settle()
    expect(reply.calls).toHaveLength(2)
  })

  it('keeps what the reader is typing when a card sends the last message again', async () => {
    const reply = stubStream()
    const wrapper = await mountChat()
    await sendMessage(wrapper, 'Add a page')
    reply.send('result', { title: 'Failed', actions: [{ label: 'Try again', resend: true }] })
    reply.close()
    await settle()
    const field = wrapper.get('[data-testid="plugin-chat-input"]')
    await field.setValue('And a blog')

    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    await settle()

    expect(reply.calls).toHaveLength(2)
    expect((field.element as HTMLTextAreaElement).value).toBe('And a blog')
    expect(wrapper.text()).toContain('Add a page')
  })
})
