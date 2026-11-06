/**
 * Tests for PluginPreviewBlock: an address on an exact frame origin is framed; a foreign origin, `javascript:`, http, the
 * panel's own origin and user info show no frame and no link; the switches; a document loaded for another page loads the
 * page in full, once.
 *
 * @module tests/components/plugin/PreviewBlock.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reactive } from 'vue'
import { mountBlocks, ROUTES_URL, startPluginApi, TEST_PLUGIN, TEST_ROUTES } from '~/tests/helpers/mountPlugin'
import { settle } from '~/tests/helpers/settle'
import type { StubServer } from '~/tests/helpers/stubServer'
import type { PluginPreviewBlock } from '~/types/pluginblock'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginPreviewSize } from '~/types/pluginpreviewsize'

/** The one origin the page may frame. */
const ORIGIN = 'https://site.example.test'

/** The stub API of the running test. */
let api: StubServer | undefined

/** The block under test. */
const preview: PluginPreviewBlock = {
  kind: PluginBlockKind.Preview,
  id: 'site-preview',
  sources: [
    { id: 'preview', label: 'Preview', route: TEST_ROUTES.site, field: 'previewUrl' },
    { id: 'live', label: 'Live', route: TEST_ROUTES.site, field: 'liveUrl' },
  ],
}

/**
 * Says which address the document was loaded from.
 *
 * @param path - The document's path.
 * @returns Nothing.
 */
const documentAt = (path: string): void => {
  vi.spyOn(performance, 'getEntriesByType').mockReturnValue([
    { name: `${window.location.origin}${path}`, serverTiming: [{ name: 'plugin-frames' }] } as unknown as PerformanceEntry,
  ])
}

/**
 * Mounts the block with the stub answering these addresses.
 *
 * @param previewUrl - The address of the first source.
 * @param liveUrl - The address of the second.
 * @returns The mounted view, its read ended.
 */
const mountPreview = async (previewUrl: unknown, liveUrl: unknown = `${ORIGIN}/live`): Promise<ReturnType<typeof mountBlocks>> => {
  api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl, liveUrl } } })
  const wrapper = mountBlocks([preview], { frameOrigins: [ORIGIN] })
  await settle()
  return wrapper
}

/** The route the block reads, reactive as the real one is, so a test can move it. */
const routed = reactive({ params: {}, query: {}, path: '/', fullPath: '/', meta: {} })

beforeEach(() => {
  sessionStorage.clear()
  documentAt(`/client/apps/${TEST_PLUGIN}/sites/7`)
  routed.path = `/client/apps/${TEST_PLUGIN}/sites/7`
  vi.stubGlobal('useRoute', () => routed)
})

afterEach(async () => {
  await api?.close()
  api = undefined
  vi.restoreAllMocks()
})

describe('PluginPreviewBlock', () => {
  it('frames an address on an allowed origin, sandboxed, with an open link', async () => {
    const wrapper = await mountPreview(`${ORIGIN}/p`)

    const frame = wrapper.get('[data-testid="plugin-preview-frame"]')
    expect(frame.attributes('src')).toBe(`${ORIGIN}/p`)
    expect(frame.attributes('title')).toBe('Preview')
    expect(frame.attributes('sandbox')).toBe('allow-scripts allow-forms')
    const open = wrapper.get('[data-testid="plugin-preview-open"]')
    expect(open.attributes('rel')).toContain('noreferrer')
  })

  it.each([
    ['a foreign origin', 'https://evil.test/'],
    ['a look-alike suffix', 'https://site.example.test.evil.test/'],
    ['a child of the origin', 'https://x.site.example.test/'],
    ['javascript:', 'javascript:alert(1)'],
    ['data:', 'data:text/html,<script>alert(1)</script>'],
    ['http in production', 'http://site.example.test/'],
    ['user info', 'https://user:pw@site.example.test/'],
    ['a relative address', '/client/services'],
    ['a value that is no string', 42],
  ])('shows no frame and no link for %s', async (_name, address) => {
    const wrapper = await mountPreview(address)

    expect(wrapper.find('iframe').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-preview-open"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-preview-address"]').exists()).toBe(false)
  })

  it('never frames the panel itself, even when the list names it', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${window.location.origin}/x` } } })
    const wrapper = mountBlocks([preview], { frameOrigins: [window.location.origin] })
    await settle()

    expect(wrapper.find('iframe').exists()).toBe(false)
  })

  it('frames an http origin only when the host listed it, as it does in development', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: 'http://localhost:8088/' } } })
    const wrapper = mountBlocks([preview], { frameOrigins: ['http://localhost:8088'] })
    await settle()

    expect(wrapper.get('iframe').attributes('src')).toBe('http://localhost:8088/')
  })

  it('switches the source and the size', async () => {
    const wrapper = await mountPreview(`${ORIGIN}/p`)

    await wrapper.get('[data-testid="plugin-preview-source-live"]').trigger('click')
    await settle()
    expect(wrapper.get('iframe').attributes('src')).toBe(`${ORIGIN}/live`)
    expect(wrapper.get('iframe').attributes('title')).toBe('Live')

    await wrapper.get('[data-testid="plugin-preview-size-phone"]').trigger('click')
    expect(wrapper.get('[data-testid="plugin-preview-viewport"]').classes()).toContain('max-w-[375px]')
  })

  it('starts at the size the view names and hides the source switch for one source', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/p` } } })
    const wrapper = mountBlocks([{ ...preview, size: PluginPreviewSize.Phone, sources: [preview.sources[0]!] }], { frameOrigins: [ORIGIN] })
    await settle()

    expect(wrapper.find('[data-testid="plugin-preview-source"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-preview-viewport"]').classes()).toContain('max-w-[375px]')
  })

  it('loads the page in full, once, when the document was served for another page of the same plugin', async () => {
    documentAt(`/client/apps/${TEST_PLUGIN}/overview`)
    const assign = vi.fn()
    vi.stubGlobal('location', { origin: window.location.origin, href: 'https://panel.test/client/apps/x', assign })

    const first = await mountPreview(`${ORIGIN}/p`)
    expect(first.find('iframe').exists()).toBe(false)
    expect(assign).toHaveBeenCalledTimes(1)
    await api?.close()

    // The loop guard: a document that still does not match frames nothing and does not load again.
    await mountPreview(`${ORIGIN}/p`)
    expect(assign).toHaveBeenCalledTimes(1)
  })

  it('takes the frame away and loads in full when a router move leaves the page the document was served for', async () => {
    const assign = vi.fn()
    const wrapper = await mountPreview(`${ORIGIN}/p`)
    expect(wrapper.find('iframe').exists()).toBe(true)
    vi.stubGlobal('location', { origin: window.location.origin, href: 'https://panel.test/client/apps/x', assign })

    routed.path = `/client/apps/${TEST_PLUGIN}/sites/8`
    await settle()

    expect(wrapper.find('iframe').exists()).toBe(false)
    expect(assign).toHaveBeenCalledTimes(1)
  })

  it('loads the page in full, once, when the document was loaded for something else, and frames nothing meanwhile', async () => {
    documentAt('/client/dashboard')
    const assign = vi.fn()
    vi.stubGlobal('location', { origin: window.location.origin, href: 'https://panel.test/client/apps/x', assign })

    const first = await mountPreview(`${ORIGIN}/p`)
    expect(first.find('iframe').exists()).toBe(false)
    expect(assign).toHaveBeenCalledTimes(1)
    await api?.close()

    await mountPreview(`${ORIGIN}/p`)
    expect(assign).toHaveBeenCalledTimes(1)
    vi.unstubAllGlobals()
  })

  it('loads the page in full once when its document holds no origins, the address being the same (a sign-in inside it)', async () => {
    const path = `/client/apps/${TEST_PLUGIN}/sites/7`
    vi.spyOn(performance, 'getEntriesByType').mockReturnValue([
      { name: `${window.location.origin}${path}`, serverTiming: [] } as unknown as PerformanceEntry,
    ])
    const assign = vi.fn()
    vi.stubGlobal('location', { ...window.location, origin: window.location.origin, href: `${window.location.origin}${path}`, assign })

    const wrapper = await mountPreview(`${ORIGIN}/stage`)

    expect(assign).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[data-testid="plugin-preview-frame"]').exists()).toBe(false)
    vi.unstubAllGlobals()
  })

  it('draws a new frame when another block refreshes it, the address unchanged, with the sandbox as it was', async () => {
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/stage`, liveUrl: `${ORIGIN}/live` } },
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: {} },
    })
    const wrapper = mountBlocks([
      preview,
      { kind: PluginBlockKind.Action, label: 'Change', route: TEST_ROUTES.publish, refresh: ['site-preview'] },
    ], { frameOrigins: [ORIGIN] })
    await settle()
    const first = wrapper.get('[data-testid="plugin-preview-frame"]').element

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()

    const second = wrapper.get('[data-testid="plugin-preview-frame"]')
    expect(second.element).not.toBe(first)
    expect(second.attributes('src')).toBe(`${ORIGIN}/stage`)
    expect(second.attributes('sandbox')).toBe('allow-scripts allow-forms')
  })

  it('writes the selected source\'s note over the frame, as text', async () => {
    api = await startPluginApi({ [`GET ${ROUTES_URL}/sites/7`]: { body: { previewUrl: `${ORIGIN}/stage`, liveUrl: `${ORIGIN}/live`, notes: { preview: 'Version 3, <b>not live</b>' } } } })
    const wrapper = mountBlocks([{ ...preview, note: { route: TEST_ROUTES.site, field: 'notes' } }], { frameOrigins: [ORIGIN] })
    await settle()

    const note = wrapper.get('[data-testid="plugin-preview-note"]')
    expect(note.text()).toBe('Version 3, <b>not live</b>')
    expect(note.find('b').exists()).toBe(false)
  })

  it('with a note, keeps the frame through a refresh and draws a new one when the note changes', async () => {
    let note = 'Version 3'
    api = await startPluginApi({
      [`GET ${ROUTES_URL}/sites/7`]: () => ({ body: { previewUrl: `${ORIGIN}/stage`, liveUrl: `${ORIGIN}/live`, notes: { preview: note } } }),
      [`POST ${ROUTES_URL}/sites/7/publish`]: { body: {} },
    })
    const wrapper = mountBlocks([
      { ...preview, note: { route: TEST_ROUTES.site, field: 'notes' } },
      { kind: PluginBlockKind.Action, label: 'Change', route: TEST_ROUTES.publish, refresh: ['site-preview'] },
    ], { frameOrigins: [ORIGIN] })
    await settle()
    const first = wrapper.get('[data-testid="plugin-preview-frame"]').element

    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(wrapper.get('[data-testid="plugin-preview-frame"]').element).toBe(first)

    note = 'Version 4'
    await wrapper.get('[data-testid="plugin-action-button"]').trigger('click')
    await settle()
    expect(wrapper.get('[data-testid="plugin-preview-frame"]').element).not.toBe(first)
  })
})
