/**
 * Tests for element selection on a preview and its chips on a chat: only this frame's window, in the exact shape, on the
 * expected release and a selectable source, with the mode on, moves anything; a label comes from the server, never the frame;
 * a new release empties the selection; a message carries what the server confirmed.
 *
 * @module tests/composables/usePluginSelection.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reactive } from 'vue'
import { PLUGIN_SELECTION_MAX_IDS, PLUGIN_SELECTION_MESSAGE_TYPE, PLUGIN_SELECTION_MESSAGE_VERSION } from '~/constants/pluginSelection'
import { mountBlocks, ROUTES_URL, startPluginApi, stubStream, TEST_PLUGIN, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { StubReply, StubRequest, StubServer } from '~/tests/helpers/stubServer'
import type { PluginBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'

/** The one origin the page may frame. */
const ORIGIN = 'https://site.example.test'
/** The release on the preview. */
const RELEASE = 'r4'
/** The element the tests point at. */
const ELEMENT = 'pages.0.title'
/** The lookup route. */
const PICK = { method: 'post', path: '/sites/{siteId}/selection' } as const
/** The chat and the preview, wired as a view declares them. */
const BLOCKS: PluginBlock[] = [
  { kind: PluginBlockKind.Chat, send: TEST_ROUTES.chat, selection: true },
  {
    kind: PluginBlockKind.Preview,
    id: 'site-preview',
    sources: [
      { id: 'preview', label: 'Preview', route: TEST_ROUTES.site, field: 'previewUrl' },
      { id: 'live', label: 'Live', route: TEST_ROUTES.site, field: 'liveUrl' },
    ],
    selection: { sources: ['preview'], route: PICK, release: { route: TEST_ROUTES.site, field: 'release' } },
  },
]

/** An action that makes the preview read again. */
const ACTION: PluginBlock = { kind: PluginBlockKind.Action, label: 'Change', route: TEST_ROUTES.publish, refresh: ['site-preview'] }

/**
 * Answers a lookup with the one known element.
 *
 * @param _request - The lookup.
 * @returns The answer.
 */
const labelled = (_request: StubRequest): StubReply => ({ body: { items: [{ id: ELEMENT, label: 'Server label' }] } })

/**
 * Answers a lookup knowing every id it was asked.
 *
 * @param request - The lookup.
 * @returns The answer.
 */
const everyId = (request: StubRequest): StubReply => ({
  body: { items: (JSON.parse(request.body) as { ids: string[] }).ids.map(id => ({ id, label: `Label ${id}` })) },
})

/**
 * Answers a lookup giving every id one label.
 *
 * @param request - The lookup.
 * @returns The answer.
 */
const sameLabel = (request: StubRequest): StubReply => ({
  body: { items: (JSON.parse(request.body) as { ids: string[] }).ids.map(id => ({ id, label: 'Same' })) },
})

/** The stub API of the running test. */
let api: StubServer | undefined
/** What the lookup was asked. */
let asked: unknown[] = []

/**
 * Mounts the view with a server that labels one element.
 *
 * @returns The view.
 */
const mountView = async (): Promise<ReturnType<typeof mountBlocks>> => {
  api = await startPluginApi({
    [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release: RELEASE } },
    [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
    [`POST ${ROUTES_URL}/sites/7/selection`]: (request) => {
      asked.push(JSON.parse(request.body))
      return { body: { releaseId: RELEASE, items: [{ id: ELEMENT, label: 'Server label' }] } }
    },
  })
  const wrapper = mountBlocks(BLOCKS, { frameOrigins: [ORIGIN], options: { attachToBody: true } })
  await settle()
  return wrapper
}

/**
 * The window the mounted frame reports, which a test stands in for by source.
 *
 * @param wrapper - The view.
 * @returns The frame element's window.
 */
const frameWindow = (wrapper: ReturnType<typeof mountBlocks>): Window =>
  (wrapper.get('[data-testid="plugin-preview-frame"]').element as HTMLIFrameElement).contentWindow as Window

/**
 * Delivers a message as a window would and lets the page react.
 *
 * @param data - The message.
 * @param source - Who it claims to come from.
 * @returns Resolves when settled.
 */
const deliver = async (data: unknown, source: Window | null): Promise<void> => {
  window.dispatchEvent(new MessageEvent('message', { data, source, origin: 'null' }))
  await settle()
}

/**
 * A well-formed message.
 *
 * @param event - The event.
 * @param extra - More members.
 * @returns The message.
 */
const message = (event: string, extra: Record<string, unknown> = {}): Record<string, unknown> => ({
  type: PLUGIN_SELECTION_MESSAGE_TYPE, version: PLUGIN_SELECTION_MESSAGE_VERSION, release: RELEASE, event, ...extra,
})

/**
 * Brings the frame to ready and turns the mode on.
 *
 * @param wrapper - The view.
 * @returns Resolves when the mode is on.
 */
const enterMode = async (wrapper: ReturnType<typeof mountBlocks>): Promise<void> => {
  await deliver(message('ready'), frameWindow(wrapper))
  await wrapper.get('[data-testid="plugin-preview-select"]').trigger('click')
  await settle()
}

/** The route the block reads. */
const routed = reactive({ params: {}, query: {}, path: `/client/apps/${TEST_PLUGIN}/sites/7`, fullPath: '/', meta: {} })

beforeEach(() => {
  asked = []
  sessionStorage.clear()
  vi.spyOn(performance, 'getEntriesByType').mockReturnValue([
    { name: `${window.location.origin}/client/apps/${TEST_PLUGIN}/sites/7`, serverTiming: [{ name: 'plugin-frames' }] } as unknown as PerformanceEntry,
  ])
  vi.stubGlobal('useRoute', () => routed)
})

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.restoreAllMocks()
})

describe('element selection', () => {
  it('offers no select button until the frame says it is ready, and keeps the sandbox', async () => {
    const wrapper = await mountView()

    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-preview-frame"]').attributes('sandbox')).toBe('allow-scripts allow-forms')

    await deliver(message('ready'), frameWindow(wrapper))
    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(true)
  })

  it('shows the server\'s label for a toggle, never text from the frame', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)

    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))

    expect(asked).toEqual([{ releaseId: RELEASE, ids: [ELEMENT] }])
    expect(wrapper.get('[data-testid="plugin-chat-selection-chip"]').text()).toContain('Server label')
  })

  it.each([
    ['from another window', () => window, message('toggle', { id: ELEMENT })],
    ['with no source', () => null, message('toggle', { id: ELEMENT })],
  ])('refuses a message %s', async (_name, source, data) => {
    const wrapper = await mountView()
    await enterMode(wrapper)

    await deliver(data, source())

    expect(asked).toEqual([])
    expect(wrapper.find('[data-testid="plugin-chat-selection-chip"]').exists()).toBe(false)
  })

  it.each([
    ['a wrong version', { ...message('toggle', { id: ELEMENT }), version: 2 }],
    ['a wrong release', { ...message('toggle', { id: ELEMENT }), release: 'r9' }],
    ['a malformed id', message('toggle', { id: '<b>x</b>' })],
    ['planted text', message('toggle', { id: ELEMENT, label: 'planted' })],
  ])('refuses %s from its own frame', async (_name, data) => {
    const wrapper = await mountView()
    await enterMode(wrapper)

    await deliver(data, frameWindow(wrapper))

    expect(asked).toEqual([])
    expect(wrapper.text()).not.toContain('planted')
  })

  it('ignores a toggle while the mode is off', async () => {
    const wrapper = await mountView()
    await deliver(message('ready'), frameWindow(wrapper))

    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))

    expect(asked).toEqual([])
  })

  it('offers nothing and takes nothing on a source the view does not let select', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)

    await wrapper.get('[data-testid="plugin-preview-source-live"]').trigger('click')
    await settle()
    await deliver(message('ready'), frameWindow(wrapper))
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))

    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(false)
    expect(asked).toEqual([])
  })

  it('leaves the mode on exit and on Esc', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    expect(wrapper.find('[data-testid="plugin-preview-select-hint"]').exists()).toBe(true)

    await deliver(message('exit'), frameWindow(wrapper))
    expect(wrapper.find('[data-testid="plugin-preview-select-hint"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-preview-select"]').trigger('click')
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await settle()
    expect(wrapper.find('[data-testid="plugin-preview-select-hint"]').exists()).toBe(false)
  })

  it('removes a chip with its button, and a second toggle removes it too', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))

    await wrapper.get('[data-testid="plugin-chat-selection-remove"]').trigger('click')
    await settle()
    expect(wrapper.find('[data-testid="plugin-chat-selection-chip"]').exists()).toBe(false)

    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    expect(wrapper.find('[data-testid="plugin-chat-selection-chip"]').exists()).toBe(false)
  })

  it('sends what the server confirmed with the message, then empties the selection', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    const seen: unknown[] = []
    const fetched = vi.spyOn(globalThis, 'fetch').mockImplementation((_url, init) => {
      seen.push(JSON.parse(String(init?.body)))
      return Promise.resolve(new Response('', { status: 200, headers: { 'Content-Type': 'text/event-stream' } }))
    })

    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Shorter')
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()

    expect(seen).toEqual([{ message: 'Shorter', selection: { releaseId: RELEASE, ids: [ELEMENT] } }])
    expect(wrapper.find('[data-testid="plugin-chat-selection-chip"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-preview-select-hint"]').exists()).toBe(false)
    fetched.mockRestore()
  })

  it('tells the frame nothing before it said it is ready', async () => {
    const wrapper = await mountView()
    const post = vi.spyOn(frameWindow(wrapper), 'postMessage')

    await wrapper.get('[data-testid="plugin-preview-source-live"]').trigger('click')
    await settle()

    expect(post).not.toHaveBeenCalled()
  })

  it('takes nothing from a source that may not select while the mode is still on', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    const source = frameWindow(wrapper)

    // The switch has moved and the watcher that ends the mode has not run yet, so only the source check stands in the way.
    const moved = wrapper.get('[data-testid="plugin-preview-source-live"]').trigger('click')
    window.dispatchEvent(new MessageEvent('message', { data: message('toggle', { id: ELEMENT }), source, origin: 'null' }))
    await moved
    await settle()

    expect(asked).toEqual([])
  })

  it('empties the selection and its mode when the release changes, and takes nothing of the old one', async () => {
    let release = RELEASE
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release } }),
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: {} },
      [`POST ${ROUTES_URL}/sites/7/selection`]: (request) => {
        asked.push(JSON.parse(request.body))
        return labelled(request)
      },
    })
    const wrapper = mountBlocks([...BLOCKS, ACTION], { frameOrigins: [ORIGIN], options: { attachToBody: true } })
    await settle()
    await enterMode(wrapper)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    expect(wrapper.find('[data-testid="plugin-chat-selection-chip"]').exists()).toBe(true)
    const lookups = asked.length

    release = 'r5'
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    expect(wrapper.find('[data-testid="plugin-chat-selection-chip"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(false)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    expect(asked).toHaveLength(lookups)
  })

  /**
   * Sends a selected message whose reply offers a "try again", then lets the plugin's release move to the one given.
   *
   * @param nextRelease - The release after the change.
   * @returns The view and the stream stub.
   */
  const failedTurn = async (nextRelease: string): Promise<{ wrapper: ReturnType<typeof mountBlocks>, reply: ReturnType<typeof stubStream> }> => {
    let release = RELEASE
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release } }),
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: {} },
      [`POST ${ROUTES_URL}/sites/7/selection`]: labelled,
    })
    const wrapper = mountBlocks([...BLOCKS, ACTION], { frameOrigins: [ORIGIN], options: { attachToBody: true } })
    await settle()
    await enterMode(wrapper)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    const reply = stubStream()
    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Shorter')
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()
    reply.send('result', { title: 'Failed', actions: [{ label: 'Try again', resend: true }] })
    reply.close()
    await settle()

    release = nextRelease
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    return { wrapper, reply }
  }

  it('resends the claim while its release is on the preview', async () => {
    const { wrapper, reply } = await failedTurn(RELEASE)

    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    await settle()

    const body = JSON.parse(String(reply.calls[1]?.init.body)) as { message: string, selection?: { ids: string[] } }
    expect(body.message).toBe('Shorter')
    expect(body.selection?.ids).toEqual([ELEMENT])
    expect(wrapper.find('[data-testid="plugin-chat-selection-stale"]').exists()).toBe(false)
    vi.unstubAllGlobals()
  })

  it('refuses a resend once the release changed: nothing is sent, the reader is told and the message waits in the field', async () => {
    const { wrapper, reply } = await failedTurn('r5')
    const messages = wrapper.findAll('[data-testid="plugin-chat-user"]').length

    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    await settle()

    expect(reply.calls).toHaveLength(1)
    expect(wrapper.findAll('[data-testid="plugin-chat-user"]')).toHaveLength(messages)
    expect(wrapper.get('[data-testid="plugin-chat-selection-stale"]').text()).toBe('plugins.chatSelectionStale')
    expect((wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('Shorter')
    vi.unstubAllGlobals()
  })

  it('keeps what the reader is typing when a resend is refused', async () => {
    const { wrapper, reply } = await failedTurn('r5')
    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Something new')

    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    await settle()

    expect(reply.calls).toHaveLength(1)
    expect((wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('Something new')
    vi.unstubAllGlobals()
  })

  it('lets the reader send again after selecting on the new release, which ends the notice', async () => {
    const { wrapper, reply } = await failedTurn('r5')
    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    await settle()

    await deliver({ ...message('ready'), release: 'r5' }, frameWindow(wrapper))
    await wrapper.get('[data-testid="plugin-preview-select"]').trigger('click')
    await settle()
    await deliver({ ...message('toggle', { id: ELEMENT }), release: 'r5' }, frameWindow(wrapper))
    expect(wrapper.find('[data-testid="plugin-chat-selection-stale"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()

    const body = JSON.parse(String(reply.calls[1]?.init.body)) as { message: string, selection?: { releaseId: string } }
    expect(body.message).toBe('Shorter')
    expect(body.selection?.releaseId).toBe('r5')
    vi.unstubAllGlobals()
  })

  it('draws one chip for an id the lookup repeats', async () => {
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release: RELEASE } },
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`POST ${ROUTES_URL}/sites/7/selection`]: { body: { items: [{ id: ELEMENT, label: 'First' }, { id: ELEMENT, label: 'Again' }] } },
    })
    const wrapper = mountBlocks(BLOCKS, { frameOrigins: [ORIGIN], options: { attachToBody: true } })
    await settle()
    await enterMode(wrapper)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))

    const chips = wrapper.findAll('[data-testid="plugin-chat-selection-chip"]')
    expect(chips).toHaveLength(1)
    expect(chips[0]?.text()).toContain('First')
  })

  it.each([
    ['takes a ready that arrived before the release was read', RELEASE, true],
    ['leaves a ready that named another release', 'r9', false],
  ])('%s', async (_name, named, offered) => {
    let answer: () => void = () => undefined
    const read = new Promise<void>((resolve) => {
      answer = resolve
    })
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: async () => {
        await read
        return { body: { release: RELEASE } }
      },
      [`GET ${ROUTES_URL}/sites/7/frame`]: { body: { url: `${ORIGIN}/p` } },
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
    })
    const blocks: PluginBlock[] = [{
      kind: PluginBlockKind.Preview,
      id: 'site-preview',
      sources: [{ id: 'preview', label: 'Preview', route: { method: 'get', path: '/sites/{siteId}/frame' }, field: 'url' }],
      selection: { sources: ['preview'], route: PICK, release: { route: TEST_ROUTES.site, field: 'release' } },
    }]
    const wrapper = mountBlocks(blocks, { frameOrigins: [ORIGIN], options: { attachToBody: true } })
    await settle()

    await deliver({ ...message('ready'), release: named }, frameWindow(wrapper))
    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(false)

    answer()
    await settle()
    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(offered)
  })

  it('removes its listeners on unmount, so a frame\'s later message moves nothing', async () => {
    const added = vi.spyOn(window, 'addEventListener')
    const wrapper = await mountView()
    await enterMode(wrapper)
    const source = frameWindow(wrapper)
    const removed = vi.spyOn(window, 'removeEventListener')

    wrapper.unmount()

    expect(removed.mock.calls.map(call => call[0])).toEqual(expect.arrayContaining(['message', 'keydown']))
    for (const type of ['message', 'keydown']) {
      const handlerAdded = added.mock.calls.find(call => call[0] === type)?.[1]
      const handlerRemoved = removed.mock.calls.find(call => call[0] === type)?.[1]
      expect(handlerAdded).toBeDefined()
      expect(handlerRemoved).toBe(handlerAdded)
    }
    await deliver(message('toggle', { id: ELEMENT }), source)
    expect(asked).toEqual([])
  })

  it('asks a frame recreated by its key to say it is ready, refuses the old window and tells the new one the state', async () => {
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release: RELEASE } },
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: {} },
      [`POST ${ROUTES_URL}/sites/7/selection`]: (request) => {
        asked.push(JSON.parse(request.body))
        return labelled(request)
      },
    })
    const wrapper = mountBlocks([...BLOCKS, ACTION], { frameOrigins: [ORIGIN], options: { attachToBody: true } })
    await settle()
    await enterMode(wrapper)
    await deliver(message('toggle', { id: ELEMENT }), frameWindow(wrapper))
    const old = frameWindow(wrapper)
    const lookups = asked.length

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    const fresh = frameWindow(wrapper)
    expect(fresh === old).toBe(false)
    expect(wrapper.find('[data-testid="plugin-preview-select"]').exists()).toBe(false)
    await deliver(message('toggle', { id: 'pages.1.title' }), old)
    expect(asked).toHaveLength(lookups)

    const post = vi.spyOn(fresh, 'postMessage')
    await deliver(message('ready'), fresh)
    expect(post).toHaveBeenCalledWith(
      { type: PLUGIN_SELECTION_MESSAGE_TYPE, version: PLUGIN_SELECTION_MESSAGE_VERSION, event: 'state', active: true, selected: [ELEMENT] }, '*')
  })

  it('refuses a toggle past the id limit without asking the server', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    const source = frameWindow(wrapper)
    api?.respondWith((request) => {
      if (request.url.endsWith('/selection')) {
        asked.push(JSON.parse(request.body))
        return everyId(request)
      }
      return { body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release: RELEASE } }
    })

    for (let index = 0; index <= PLUGIN_SELECTION_MAX_IDS; index += 1) {
      await deliver(message('toggle', { id: `pages.${index}` }), source)
    }

    const last = asked.at(-1) as { ids: string[] }
    expect(last.ids).toHaveLength(PLUGIN_SELECTION_MAX_IDS)
    expect(last.ids).not.toContain(`pages.${PLUGIN_SELECTION_MAX_IDS}`)
    expect(wrapper.findAll('[data-testid="plugin-chat-selection-chip"]')).toHaveLength(PLUGIN_SELECTION_MAX_IDS)
  })

  it('keeps both of two quick toggles and bounds a flood to one lookup in flight and one after it', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    const source = frameWindow(wrapper)
    /** Lets the held lookup answer. */
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => {
      release = resolve
    })
    api?.respondWith(async (request) => {
      if (!request.url.endsWith('/selection')) {
        return { body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release: RELEASE } }
      }
      asked.push(JSON.parse(request.body))
      await held
      return everyId(request)
    })

    await deliver(message('toggle', { id: 'pages.0' }), source)
    await deliver(message('toggle', { id: 'pages.1' }), source)
    for (let index = 0; index < 50; index += 1) {
      window.dispatchEvent(new MessageEvent('message', { data: message('toggle', { id: 'pages.2' }), source, origin: 'null' }))
    }
    await settle()
    expect(asked).toHaveLength(1)

    release()
    await settle()

    expect(asked).toEqual([{ releaseId: RELEASE, ids: ['pages.0'] }, { releaseId: RELEASE, ids: ['pages.0', 'pages.1'] }])
    expect(wrapper.findAll('[data-testid="plugin-chat-selection-chip"]')).toHaveLength(2)
  })

  it('renders two chips and two sent badges for elements with the same label', async () => {
    const wrapper = await mountView()
    await enterMode(wrapper)
    api?.respondWith(request => (request.url.endsWith('/selection') ? sameLabel(request) : { body: {} }))
    await deliver(message('toggle', { id: 'pages.0' }), frameWindow(wrapper))
    await deliver(message('toggle', { id: 'pages.1' }), frameWindow(wrapper))
    expect(wrapper.findAll('[data-testid="plugin-chat-selection-chip"]')).toHaveLength(2)
    stubStream()

    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Shorter')
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()

    expect(wrapper.get('[data-testid="plugin-chat-message-selection"]').text()).toBe('SameSame')
    vi.unstubAllGlobals()
  })
})

describe('element selection on two previews of one page', () => {
  /** Two previews that select, then one that declares no selection, and the chat. */
  const TWO: PluginBlock[] = [
    { kind: PluginBlockKind.Chat, send: TEST_ROUTES.chat, selection: true },
    { kind: PluginBlockKind.Preview, id: 'one', sources: [{ id: 'a', label: 'A', route: TEST_ROUTES.site, field: 'previewUrl' }], selection: { sources: ['a'], route: PICK, release: { route: TEST_ROUTES.site, field: 'release' } } },
    { kind: PluginBlockKind.Preview, id: 'two', sources: [{ id: 'b', label: 'B', route: TEST_ROUTES.site, field: 'previewUrl' }], selection: { sources: ['b'], route: PICK, release: { route: TEST_ROUTES.site, field: 'release' } } },
    { kind: PluginBlockKind.Preview, id: 'plain', sources: [{ id: 'c', label: 'C', route: TEST_ROUTES.site, field: 'previewUrl' }, { id: 'd', label: 'D', route: TEST_ROUTES.site, field: 'liveUrl' }] },
  ]

  /**
   * Mounts the page.
   *
   * @returns The view and each preview's window.
   */
  const mountTwo = async (): Promise<{ wrapper: ReturnType<typeof mountBlocks>, one: Window, two: Window, plain: Window }> => {
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/p`, liveUrl: `${ORIGIN}/l`, release: RELEASE } },
      [`GET ${ROUTES_URL}/sites/7/history`]: { body: [] },
      [`POST ${ROUTES_URL}/sites/7/selection`]: (request) => {
        asked.push(JSON.parse(request.body))
        return everyId(request)
      },
    })
    const wrapper = mountBlocks(TWO, { frameOrigins: [ORIGIN], options: { attachToBody: true } })
    await settle()
    const [one, two, plain] = wrapper.findAll('[data-testid="plugin-preview-frame"]').map(frame => (frame.element as HTMLIFrameElement).contentWindow as Window)
    return { wrapper, one: one as Window, two: two as Window, plain: plain as Window }
  }

  /**
   * Readies a preview and turns its mode on.
   *
   * @param wrapper - The view.
   * @param source - The preview's window.
   * @param index - Its place among the select buttons once ready.
   * @returns Resolves when the mode is on.
   */
  const enter = async (wrapper: ReturnType<typeof mountBlocks>, source: Window, index: number): Promise<void> => {
    await deliver(message('ready'), source)
    await wrapper.findAll('[data-testid="plugin-preview-select"]')[index]?.trigger('click')
    await settle()
  }

  it('keeps each preview\'s ids to itself, the chat showing the one last selected on', async () => {
    const { wrapper, one, two } = await mountTwo()
    await enter(wrapper, one, 0)
    await deliver(message('toggle', { id: 'pages.1' }), one)
    await enter(wrapper, two, 1)
    await deliver(message('toggle', { id: 'pages.2' }), two)

    expect(asked).toEqual([{ releaseId: RELEASE, ids: ['pages.1'] }, { releaseId: RELEASE, ids: ['pages.2'] }])
    expect(wrapper.get('[data-testid="plugin-chat-selection"]').text()).toContain('Label pages.2')
    expect(wrapper.get('[data-testid="plugin-chat-selection"]').text()).not.toContain('Label pages.1')

    await deliver(message('toggle', { id: 'pages.3' }), one)
    expect(asked.at(-1)).toEqual({ releaseId: RELEASE, ids: ['pages.1', 'pages.3'] })
    expect(wrapper.get('[data-testid="plugin-chat-selection"]').text()).toContain('Label pages.1')
  })

  it('leaves the other preview\'s mode and selection when one sends', async () => {
    const { wrapper, one, two } = await mountTwo()
    await enter(wrapper, one, 0)
    await deliver(message('toggle', { id: 'pages.1' }), one)
    await enter(wrapper, two, 1)
    await deliver(message('toggle', { id: 'pages.2' }), two)
    stubStream()

    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Shorter')
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await settle()

    await deliver(message('toggle', { id: 'pages.3' }), one)
    expect(asked.at(-1)).toEqual({ releaseId: RELEASE, ids: ['pages.1', 'pages.3'] })
    vi.unstubAllGlobals()
  })

  it('does not let a preview without a selection post, take a message or clear anything', async () => {
    const { wrapper, one, plain } = await mountTwo()
    const post = vi.spyOn(plain, 'postMessage')
    await enter(wrapper, one, 0)
    await deliver(message('toggle', { id: 'pages.1' }), one)

    await deliver(message('ready'), plain)
    await deliver(message('toggle', { id: 'pages.9' }), plain)
    await wrapper.get('[data-testid="plugin-preview-source-d"]').trigger('click')
    await settle()

    expect(post).not.toHaveBeenCalled()
    expect(wrapper.findAll('[data-testid="plugin-preview-select"]')).toHaveLength(1)
    expect(asked).toEqual([{ releaseId: RELEASE, ids: ['pages.1'] }])
    expect(wrapper.findAll('[data-testid="plugin-chat-selection-chip"]')).toHaveLength(1)
  })

  it('keeps one preview\'s selection when the other leaves the page', async () => {
    const { wrapper, one, two } = await mountTwo()
    await enter(wrapper, one, 0)
    await deliver(message('toggle', { id: 'pages.1' }), one)
    await enter(wrapper, two, 1)

    await wrapper.setProps({ view: { schema: 1, blocks: TWO.filter(block => block.id !== 'two') } })
    await settle()

    await deliver(message('toggle', { id: 'pages.3' }), one)
    expect(asked.at(-1)).toEqual({ releaseId: RELEASE, ids: ['pages.1', 'pages.3'] })
    expect(wrapper.findAll('[data-testid="plugin-chat-selection-chip"]')).toHaveLength(2)
  })
})
