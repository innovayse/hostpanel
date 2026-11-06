/**
 * Tests for element selection on a preview and its chips on a chat: only this frame's window, in the exact shape, on the
 * expected release and a selectable source, with the mode on, moves anything; a label comes from the server, never the frame;
 * a new release empties the selection; a message carries what the server confirmed.
 *
 * @module tests/composables/usePluginSelection.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reactive } from 'vue'
import { PLUGIN_SELECTION_MESSAGE_TYPE, PLUGIN_SELECTION_MESSAGE_VERSION } from '~/constants/pluginSelection'
import { mountBlocks, ROUTES_URL, startPluginApi, TEST_PLUGIN, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { StubServer } from '~/tests/helpers/stubServer'
import type { PluginBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'

/** The one origin the page may frame. */
const ORIGIN = 'https://site.example.test'
/** The release on the preview. */
const RELEASE = 'r4'
/** The element the tests point at. */
const ELEMENT = 'pages.0.title'
/** The lookup route. */
const PICK = { method: 'post', path: '/sites/{siteId}/publish' } as const
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
    [`POST ${ROUTES_URL}/sites/7/publish`]: (request) => {
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
})
