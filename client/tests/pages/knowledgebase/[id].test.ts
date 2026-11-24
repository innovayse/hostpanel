/**
 * Tests for the knowledge-base article page against the real content store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the body is
 * rebuilt from an allow-list of tags (never injected), the helpful vote is local, and the failure
 * and loading states.
 *
 * @module tests/pages/knowledgebase/[id].test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import Page from '~/pages/knowledgebase/[id].vue'
import { kbArticlePath } from '~/tests/helpers/contentPaths'
import { SCHEMA_ORG_CONTEXT } from '~/constants/schemaOrg'
import { PUBLIC_PAGE_BASE_URL, mountPage, publishedSchemas, seoTags, stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { makeBackendUnreachable } from '~/tests/helpers/unreachableTransport'
import { SeoPageType } from '~/types/seopagetype'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** An article whose body carries markup that must not survive. */
const ARTICLE = {
  id: 5,
  title: 'Point a domain at your server',
  excerpt: 'Change the DNS records',
  views: 12,
  dateupdated: '2026-02-01',
  body: '<h2>Steps</h2><p>Open the <a href="javascript:alert(1)">panel</a>.</p><script>window.hacked = true</script>',
}

/** The route path of the article, which the canonical URL is built from. */
const PAGE_PATH = '/knowledgebase/5'

/** The key of the article read in a route table. */
const ARTICLE_READ = `GET ${kbArticlePath('5')}`

/** The root element of the page. */
const ROOT = 'kb-article-page'

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

describe('pages/knowledgebase/[id]', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime({ params: { id: '5' }, path: PAGE_PATH })
    table = { [ARTICLE_READ]: { body: ARTICLE } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the title, the update date and the view count', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-article-heading"]').text()).toBe('Point a domain at your server')
    expect(wrapper.find('[data-testid="kb-article-updated"]').text()).toContain('2026-02-01')
    expect(wrapper.find('[data-testid="kb-article-views"]').text()).toContain('12 kb.views')
  })

  it('renders the body through the allow-list without scripts or unsafe links', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    const body = wrapper.find('[data-testid="ui-rich-text"]')
    expect(body.find('h2').text()).toBe('Steps')
    expect(body.text()).toContain('Open the panel.')
    expect(body.find('script').exists()).toBe(false)
    expect(body.html()).not.toContain('javascript:')
  })

  it('links back to the knowledge base', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-back"]').attributes('href')).toBe('/en/knowledgebase')
    expect(wrapper.find('[data-testid="kb-back"]').text()).toBe('common.back')
    expect(wrapper.find('[data-testid="kb-back"]').attributes('aria-label')).toBe('kb.backToKb')
  })

  it('sets the head from the article, with a plain separator in the title', async () => {
    await mountRenderedPage(Page, ROOT)

    const tags = seoTags(spies)
    expect({ title: tags.title, description: tags.description, ogType: tags.ogType }).toEqual({
      title: 'Point a domain at your server | kb.title',
      description: 'Change the DNS records',
      ogType: SeoPageType.Article,
    })
    expect(tags.ogUrl).toBe(`${PUBLIC_PAGE_BASE_URL}${PAGE_PATH}`)
    expect(String(tags.title)).not.toMatch(/[–—]/)
    // The page sets its tags only through the one SEO composable, never with a `useSeoMeta` call of its own.
    expect(spies.useSeoMeta).toHaveBeenCalledTimes(1)
  })

  it('publishes Article structured data for the article at its canonical URL', async () => {
    await mountRenderedPage(Page, ROOT)

    const url = `${PUBLIC_PAGE_BASE_URL}${PAGE_PATH}`
    expect(publishedSchemas(spies)).toEqual([
      {
        '@context': SCHEMA_ORG_CONTEXT,
        '@type': 'Article',
        headline: 'Point a domain at your server',
        mainEntityOfPage: url,
        url,
        publisher: { '@id': `${PUBLIC_PAGE_BASE_URL}/#organization` },
        description: 'Change the DNS records',
      },
    ])
  })

  it('highlights the chosen vote only', async () => {
    const wrapper = await mountRenderedPage(Page, ROOT)

    await wrapper.find('[data-testid="kb-vote-yes"]').trigger('click')
    expect(wrapper.find('[data-testid="kb-vote-yes"]').classes()).toContain('bg-primary-700')
    expect(wrapper.find('[data-testid="kb-vote-no"]').classes()).not.toContain('bg-red-600')

    await wrapper.find('[data-testid="kb-vote-no"]').trigger('click')
    expect(wrapper.find('[data-testid="kb-vote-no"]').classes()).toContain('bg-red-600')
    expect(wrapper.find('[data-testid="kb-vote-yes"]').classes()).not.toContain('bg-primary-700')
  })

  it.each([
    ['a 404 from the backend', async () => {
      table[ARTICLE_READ] = { status: 404, body: { detail: 'Not found.' } }
    }, 404],
    ['a 503 from the backend', async () => {
      table[ARTICLE_READ] = { status: 503, body: { detail: 'Unavailable.' } }
    }, 503],
    ['a network error', makeBackendUnreachable, 502],
  ])('raises the right status for %s', async (_name, arrange, status) => {
    await arrange()
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const errorHandler = vi.fn()

    const wrapper = await mountPage(Page, { errorHandler })
    await waitUntil(() => expect(errorHandler).toHaveBeenCalled())

    expect(spies.createError).toHaveBeenCalledWith(expect.objectContaining({ statusCode: status }))
    expect(errorHandler).toHaveBeenCalledWith(expect.objectContaining({ statusCode: status }), expect.anything(), expect.anything())
    expect(wrapper.find('[data-testid="kb-article-heading"]').exists()).toBe(false)
    consoleError.mockRestore()
    consoleWarn.mockRestore()
  })

  it('shows placeholders while the article loads', async () => {
    vi.stubGlobal('useAsyncData', async () => ({ pending: ref(true) }))

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="kb-article-loading"]').exists()).toBe(true)
  })
})
