/**
 * Mount tests for PluginPageView: the page found by its address (the plugin root and a page with a
 * parameter whose value reaches the blocks), the page title unless the view brings a header, the
 * not-found state with the API's sentence, a failure with a retry, and a new read when the address changes.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginPageView from '@/views/PluginPageView.vue'
import { PluginBlockKind } from '@/types/pluginview'
import type { PluginBlock } from '@/types/pluginview'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { requestsOf } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * A page answer of the sample plugin.
 *
 * @param path - The page's path template.
 * @param blocks - Its blocks.
 * @returns The page.
 */
const page = (path: string, blocks: PluginBlock[]) => ({
  pluginId: 'sites', id: path === '/' ? 'home' : 'site', path, title: 'Sites', view: { schema: 1, blocks }, externalOrigins: [],
})

/**
 * Stubs the API: the index, and the page lookup answered by the given function.
 *
 * @param lookup - Answers the page lookup, given its `path` query value.
 * @param routes - More answers by `METHOD /path`.
 * @returns The fetch mock.
 */
const stubApi = (lookup: (path: string) => Response, routes: Record<string, () => Response> = {}) => {
  const mock = vi.fn((url: string, init?: RequestInit) => {
    const address = new URL(String(url), 'http://h')
    if (address.pathname === '/api/admin/plugins/ui') {
      return Promise.resolve(json({ menus: [], pages: [] }))
    }
    if (address.pathname.endsWith('/pages/by-path')) {
      return Promise.resolve(lookup(address.searchParams.get('path') ?? ''))
    }
    const handler = routes[`${init?.method ?? 'GET'} ${address.pathname.replace(/^\/api/, '')}`]
    return Promise.resolve(handler ? handler() : new Response(null, { status: 404 }))
  })
  vi.stubGlobal('fetch', mock)
  return mock
}

/**
 * Mounts the page at an address.
 *
 * @param url - The admin address.
 * @returns The wrapper and the router.
 */
const mountPage = async (url: string) => {
  const mounted = await mountRouted(PluginPageView, '/apps/:plugin/:page*', url)
  await flushPromises()
  await flushPromises()
  return mounted
}

describe('PluginPageView', () => {
  it('renders the plugin root page under its title', async () => {
    const mock = stubApi(() => json(page('/', [{ kind: PluginBlockKind.Text, text: 'Welcome' }])))
    const { wrapper } = await mountPage('/apps/sites')

    expect(wrapper.get('[data-testid="plugin-page-title"]').text()).toContain('Sites')
    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('Welcome')
    expect(requestsOf(mock)).toContain('GET /admin/plugins/ui/sites/pages/by-path?path=%2F')
  })

  it('fills a route of the view with the value the address carries, and leaves the title to the view header', async () => {
    stubApi(
      () => json(page('/sites/{siteId}', [
        { kind: PluginBlockKind.Header, title: 'Site' },
        { kind: PluginBlockKind.Stat, label: 'Visits', source: { route: { method: 'get', path: '/sites/{siteId}/stats' }, field: 'visits' } },
      ])),
      { 'GET /admin/plugins/sites/ext/sites/7/stats': () => json({ visits: 12 }) },
    )
    const { wrapper } = await mountPage('/apps/sites/sites/7')

    expect(wrapper.find('[data-testid="plugin-page-title"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-stat"]').text()).toContain('12')
  })

  it('shows the not-found state with the API sentence', async () => {
    stubApi(() => json({ type: 'PLUGIN_PAGE_NOT_FOUND', title: 'Not found', status: 404, detail: 'No such page.' }, 404))
    const { wrapper } = await mountPage('/apps/sites/nope')

    const notFound = wrapper.get('[data-testid="plugin-page-not-found"]')
    expect(notFound.text()).toContain('Page not found')
    expect(notFound.text()).toContain('No such page.')
  })

  it('shows a failure with a retry that reads the page again', async () => {
    let calls = 0
    stubApi(() => {
      calls += 1
      return calls === 1 ? problem('The server is busy.', 503) : json(page('/', [{ kind: PluginBlockKind.Text, text: 'Back' }]))
    })
    const { wrapper } = await mountPage('/apps/sites')
    expect(wrapper.get('[data-testid="plugin-page-error"]').text()).toContain('The server is busy.')

    await wrapper.get('[data-testid="plugin-page-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('Back')
  })

  it('reads the page again when the address changes', async () => {
    stubApi((path) => json(page(path === '/' ? '/' : '/usage', [{ kind: PluginBlockKind.Text, text: path }])))
    const { wrapper, router } = await mountPage('/apps/sites')
    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('/')

    await router.push('/apps/sites/usage')
    await flushPromises()
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-text"]').text()).toBe('/usage')
  })
})
