/**
 * Tests for `pages/client/apps/[plugin]/[...page].vue`, through the real store, API composable and
 * transport against a stub server: the page the address names is read through `useAsyncData` before
 * the page renders, its blocks' routes are filled from the address, the title follows the page, a
 * missing page shows the API's own sentence and no view, and a failed read offers a retry.
 *
 * @module tests/pages/client/apps/[plugin]/[...page].test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { startTransport } from '~/tests/helpers/stubTransport'
import type { StubReply, StubRequest, StubServer } from '~/tests/helpers/stubServer'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import { PLUGIN_COMPONENTS } from '~/tests/helpers/mountPlugin'
import PluginPage from '~/pages/client/apps/[plugin]/[...page].vue'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The page view the BFF answers for `/sites/{siteId}`: a header and one stat read from the site. */
const SITE_PAGE = {
  pluginId: 'acme-sites',
  id: 'site',
  path: '/sites/{siteId}',
  title: 'My site',
  externalOrigins: [],
  view: {
    schema: 1,
    blocks: [
      { kind: PluginBlockKind.Header, title: 'My site' },
      { kind: PluginBlockKind.Stat, label: 'Status', source: { route: { method: 'get', path: '/sites/{siteId}' }, field: 'status' } },
    ],
  },
}

/** The index the BFF answers, which the page's links resolve against. */
const INDEX = {
  menus: [],
  pages: [{ pluginId: 'acme-sites', id: 'site', path: '/sites/{siteId}', title: 'My site' }],
}

/**
 * Whether a request reads a plugin page, whichever of the page endpoints the BFF serves it from.
 *
 * @param request - The request the stub received.
 * @returns True for a page read of the test plugin.
 */
const isPageRead = (request: StubRequest): boolean =>
  request.method === 'GET' && /^\/api\/portal\/client\/plugins\/acme-sites\/pages?(\/|\?|$)/.test(request.url)

/**
 * The BFF's answers: the index, the page (or `page` when given), and the site's status for a route call.
 *
 * @param page - The answer to a page read.
 * @returns The stub's reply function.
 */
const answering = (page: () => StubReply = () => ({ body: SITE_PAGE })) => (request: StubRequest): StubReply => {
  if (request.url === '/api/portal/client/plugins/ui') {
    return { body: INDEX }
  }
  if (isPageRead(request)) {
    return page()
  }
  return { body: { status: 'live' } }
}

/** What the page shows once its read has ended: the view, the not-found state or the failure with its retry. */
const SETTLED = '[data-testid="plugin-view"], [data-testid="plugin-page-not-found"], [data-testid="plugin-page-error"]'

/**
 * Mounts the page and waits until its read has ended. The read goes over a real socket to the stub, so the page's
 * async setup resolves after the mount's own promise flush.
 *
 * @returns The mounted page, rendered.
 */
const mountPluginPage = async (): Promise<Awaited<ReturnType<typeof mountPage>>> => {
  const wrapper = await mountPage(PluginPage, { components: PLUGIN_COMPONENTS })
  await waitUntil(() => expect(wrapper.find(SETTLED).exists()).toBe(true))
  return wrapper
}

/** The `useHead` spy of the stood-in runtime. */
let useHead: ReturnType<typeof vi.fn>

beforeEach(async () => {
  stub = await startTransport(answering())
  ;({ useHead } = stubClientRuntime({ params: { plugin: 'acme-sites', page: ['sites', 'blog'] } as unknown as Record<string, string> }))
})

afterEach(async () => {
  await stub.close()
})

describe('plugin page', () => {
  it('reads the page the address names before it renders, and fills its routes from the address', async () => {
    const wrapper = await mountPluginPage()

    expect(stub.requests.some(isPageRead)).toBe(true)
    await waitUntil(() => expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('live'))
    expect(stub.requests.some(request => request.url === '/api/portal/client/plugins/acme-sites/routes/sites/blog')).toBe(true)
    expect(wrapper.find('[data-testid="plugin-view"]').exists()).toBe(true)
  })

  it('names the browser tab after the page title the API sent', async () => {
    await mountPluginPage()

    const head = useHead.mock.calls.at(-1)?.[0] as (() => { title?: string }) | { title?: string }
    expect(typeof head === 'function' ? head().title : head.title).toBe('My site')
  })

  it('renders the plugin\'s text as text, never as markup', async () => {
    stub.respondWith(answering(() => ({
      body: { ...SITE_PAGE, view: { schema: 1, blocks: [{ kind: PluginBlockKind.Header, title: '<img src=x onerror="alert(1)">' }] } },
    })))

    const wrapper = await mountPluginPage()

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).toContain('<img src=x onerror="alert(1)">')
  })

  it('draws a slim link back to the service on a workspace page, and passes the frame origins on', async () => {
    stub.respondWith(answering(() => ({ body: { ...SITE_PAGE, path: '/services/{serviceId}', layout: 'workspace', frameOrigins: ['https://a.example'] } })))

    const wrapper = await mountPluginPage()

    expect(wrapper.get('[data-testid="plugin-page-back"]').attributes('href')).toBe('/client/services/blog')
  })

  it('goes back to the dashboard from a workspace page with no service', async () => {
    stub.respondWith(answering(() => ({ body: { ...SITE_PAGE, layout: 'workspace', frameOrigins: [] } })))

    const wrapper = await mountPluginPage()

    expect(wrapper.get('[data-testid="plugin-page-back"]').attributes('href')).toBe('/client/dashboard')
  })

  it.each(['standard', 'unknown-layout'])('draws no back link on a page whose layout is %s', async (layout) => {
    stub.respondWith(answering(() => ({ body: { ...SITE_PAGE, layout, frameOrigins: [] } })))

    const wrapper = await mountPluginPage()

    expect(wrapper.find('[data-testid="plugin-page-back"]').exists()).toBe(false)
  })

  it('shows the API\'s sentence for a page that is not there, and no view', async () => {
    stub.respondWith(answering(() => ({ status: 404, body: { statusMessage: 'This page does not exist.', data: { code: 'PLUGIN_PAGE_NOT_FOUND' } } })))

    const wrapper = await mountPluginPage()

    expect(wrapper.get('[data-testid="plugin-page-not-found"]').text()).toContain('This page does not exist.')
    expect(wrapper.find('[data-testid="plugin-view"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-page-retry"]').exists()).toBe(false)
    expect(stub.requests.some(request => request.url.includes('/routes/'))).toBe(false)
  })

  it('offers a retry after a failed read, and renders the page once it reads', async () => {
    let reads = 0
    stub.respondWith(answering(() => {
      reads += 1
      // The transport retries a failed GET once on a 5xx, so the first read is two refused requests.
      return reads <= 2
        ? { status: 503, body: { statusMessage: 'Try again shortly.', data: { code: 'IDENTITY_PLUGIN_UNAVAILABLE' } } }
        : { body: SITE_PAGE }
    }))

    const wrapper = await mountPluginPage()
    expect(wrapper.get('[data-testid="plugin-page-error"]').text()).toContain('Try again shortly.')
    expect(wrapper.find('[data-testid="plugin-page-not-found"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-page-retry"]').trigger('click')

    await waitUntil(() => expect(wrapper.find('[data-testid="plugin-view"]').exists()).toBe(true))
    expect(reads).toBe(3)
  })
})
