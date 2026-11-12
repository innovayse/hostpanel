/**
 * Tests for the announcements list page against the real content store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the list read
 * through the content store, month filters built from the publication dates, and the loading, error
 * and empty states.
 *
 * @module tests/pages/announcements/index.test
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import Page from '~/pages/announcements/index.vue'
import { ContentPath } from '~/tests/helpers/contentPaths'
import { stubPublicPageRuntime } from '~/tests/helpers/publicPage'
import type { PublicPageSpies } from '~/tests/helpers/publicPage'
import { mountRenderedPage } from '~/tests/helpers/renderedPage'
import { routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'

/** Two announcements in different months. */
const TWO_MONTHS = {
  items: [
    { id: 11, title: 'Maintenance window', publishedAt: '2026-03-15T12:00:00Z', content: '<p>x</p>' },
    { id: 12, title: 'New data centre', publishedAt: '2026-02-10T12:00:00Z', content: '<p>y</p>' },
  ],
}

/** The root element of the page. */
const ROOT = 'announcements-page'

/** The key of the list read in a route table. */
const LIST_READ = `GET ${ContentPath.Announcements}`

/** Spies of the stood-in runtime, renewed before each test. */
let spies: PublicPageSpies

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test. */
let table: StubRouteTable

describe('pages/announcements/index', () => {
  beforeEach(async () => {
    spies = stubPublicPageRuntime()
    vi.stubGlobal('useCookie', () => ref(null))
    table = {}
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the hero and sets the page title', async () => {
    table[LIST_READ] = { body: { items: [] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('h1').text()).toBe('announcements.title')
    expect(spies.useSeoMeta).toHaveBeenCalledWith({
      title: 'announcements.title',
      description: 'announcements.subtitle',
    })
  })

  it('lists each announcement as a link to its page with the formatted date', async () => {
    table[LIST_READ] = { body: TWO_MONTHS }

    const wrapper = await mountRenderedPage(Page, ROOT)

    const first = wrapper.find('[data-testid="announcement-11"]')
    expect(first.attributes('href')).toBe('/en/announcements/11')
    expect(first.text()).toContain('Maintenance window')
    expect(first.text()).toContain('Mar 15, 2026')
    expect(wrapper.findAll('[data-testid^="announcement-"]')).toHaveLength(2)
  })

  it('offers a month filter and narrows the list to the chosen month', async () => {
    table[LIST_READ] = { body: TWO_MONTHS }
    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcements-months"]').exists()).toBe(true)
    await wrapper.find('[data-testid="announcements-month-February 2026"]').trigger('click')

    expect(wrapper.find('[data-testid="announcement-12"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="announcement-11"]').exists()).toBe(false)

    await wrapper.find('[data-testid="announcements-month-all"]').trigger('click')
    expect(wrapper.findAll('[data-testid^="announcement-"]')).toHaveLength(2)
  })

  it('offers no month filter when every announcement is from one month', async () => {
    table[LIST_READ] = { body: { items: [TWO_MONTHS.items[0]] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcements-months"]').exists()).toBe(false)
  })

  it('shows the empty state when there is nothing to list', async () => {
    table[LIST_READ] = { body: { items: [] } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcements-empty"]').text()).toContain('announcements.empty')
  })

  it('shows the error alert when a signed-in visitor is refused the list', async () => {
    vi.stubGlobal('useCookie', () => ref('1'))
    table[LIST_READ] = { status: 400, body: { detail: 'Content unavailable.' } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcements-error"]').text()).toContain('announcements.errorDesc')
    expect(wrapper.find('[data-testid="announcements-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="announcements-sign-in"]').exists()).toBe(false)
  })

  it('invites a signed-out visitor to sign in instead of showing an error', async () => {
    table[LIST_READ] = { status: 401, body: { detail: 'Not authorized.' } }

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcements-sign-in"]').text()).toContain('announcements.signInRequired')
    expect(wrapper.find('[data-testid="announcements-sign-in-link"]').attributes('href')).toBe('/en/client/login?redirect=/announcements')
    expect(wrapper.find('[data-testid="announcements-error"]').exists()).toBe(false)
  })

  it('shows placeholders while the list loads', async () => {
    table[LIST_READ] = { body: { items: [] } }
    vi.stubGlobal('useAsyncData', async () => {
      await flushPromises()
      return { pending: ref(true) }
    })

    const wrapper = await mountRenderedPage(Page, ROOT)

    expect(wrapper.find('[data-testid="announcements-loading"]').findAll('[data-testid="ui-skeleton"]')).toHaveLength(4)
  })
})
