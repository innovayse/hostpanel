/**
 * Tests for the announcement detail page against the real content store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the body is
 * rebuilt from an allow-list of tags (never injected), and the not-found and loading states.
 *
 * @module tests/pages/announcements/[id].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import Page from '~/pages/announcements/[id].vue'
import { ContentPath, announcementPath } from '~/tests/helpers/contentPaths'
import { mountPage, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { makeBackendUnreachable } from '~/tests/helpers/unreachableTransport'
import { SeoPageType } from '~/types/seopagetype'

/** An announcement whose body carries markup that must not survive. */
const ANNOUNCEMENT = {
  id: 11,
  title: 'Maintenance window',
  publishedAt: '2026-03-15T12:00:00Z',
  content: '<p>We will <strong>restart</strong> servers.</p><script>window.hacked = true</script><img src="x" onerror="alert(1)">',
}

/** The root element of the page. */
const ROOT = 'announcement-page'

/** The key of the announcement read in a route table. */
const ANNOUNCEMENT_READ = `GET ${announcementPath('11')}`

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

describe('pages/announcements/[id]', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime({ params: { id: '11' } })
    table = { [ANNOUNCEMENT_READ]: { body: ANNOUNCEMENT } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the title, the publication date and a link back to the list', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcement-heading"]').text()).toBe('Maintenance window')
    expect(wrapper.find('[data-testid="announcement-date"]').text()).toContain('Mar 15, 2026')
    expect(wrapper.find('[data-testid="announcement-back"]').attributes('href')).toBe('/en/announcements')
  })

  it('renders the body through the allow-list and drops scripts and handlers', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    const body = wrapper.find('[data-testid="ui-rich-text"]')
    expect(body.text()).toContain('We will restart servers.')
    expect(body.find('strong').text()).toBe('restart')
    expect(body.find('script').exists()).toBe(false)
    expect(body.html()).not.toContain('onerror')
  })

  it('reads the announcement by its route id from the detail endpoint', async () => {
    await mountRenderedPage(Page, ROOT)

    expect(requestsTo(stub.requests, announcementPath('11'))).toHaveLength(1)
    expect(requestsTo(stub.requests, ContentPath.Announcements)).toHaveLength(0)
  })

  it('gives the head a getter with a plain separator and a text-only description', async () => {
    await mountRenderedPage(Page, ROOT)

    const options = (spies.useSeo.mock.calls[0]![0] as () => Record<string, unknown>)()
    expect(options).toEqual({
      title: 'Maintenance window | announcements.title',
      description: 'We will restart servers.',
      type: SeoPageType.Article,
    })
    expect(String(options.title)).not.toMatch(/[–—]/)
    expect(spies.useSeoMeta).not.toHaveBeenCalled()
  })

  it('publishes Article structured data with the publication date', async () => {
    await mountRenderedPage(Page, ROOT)

    const schema = (spies.injectSchema.mock.calls[0]![0] as () => object[])()
    expect(schema).toEqual([
      {
        '@type': 'Article',
        headline: 'Maintenance window',
        description: 'We will restart servers.',
        url: 'https://portal.test/canonical',
        datePublished: '2026-03-15T12:00:00Z',
      },
    ])
  })

  it.each([
    ['an unknown or unpublished id', async () => {
      table[ANNOUNCEMENT_READ] = { status: 404, body: { detail: 'Not found.' } }
    }, 404],
    ['an empty answer', async () => {
      table[ANNOUNCEMENT_READ] = { body: null }
    }, 404],
    ['a 503 from the backend', async () => {
      table[ANNOUNCEMENT_READ] = { status: 503, body: { detail: 'Unavailable.' } }
    }, 503],
    ['a network error', makeBackendUnreachable, 502],
  ])('raises the right status for %s', async (_name, arrange, status) => {
    await arrange()
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const errorHandler = vi.fn()

    const wrapper = await mountPage(Page, { errorHandler })
    await vi.waitFor(() => expect(errorHandler).toHaveBeenCalled())

    expect(spies.createError).toHaveBeenCalledWith(expect.objectContaining({ statusCode: status }))
    expect(errorHandler).toHaveBeenCalledWith(expect.objectContaining({ statusCode: status }), expect.anything(), expect.anything())
    expect(wrapper.find('[data-testid="announcement-heading"]').exists()).toBe(false)
    consoleError.mockRestore()
    consoleWarn.mockRestore()
  })

  it('shows placeholders while the announcement loads', async () => {
    vi.stubGlobal('useAsyncData', async () => ({ pending: ref(true) }))

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcement-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="announcement-heading"]').exists()).toBe(false)
  })
})
