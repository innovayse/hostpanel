/**
 * Smoke tests for PredefinedRepliesView: the category list, adding a category, the reply tab, the
 * debounced search and the delete confirmation, through the real store and transport.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PredefinedRepliesView from '@/views/PredefinedRepliesView.vue'
import { mountView } from '../helpers/mount'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'

/** One category as the endpoint returns it. */
const category = { id: 1, name: 'Greetings', parentCategoryId: null, replyCount: 1 }

/** One reply as the endpoint returns it. */
const reply = { id: 4, name: 'Welcome', content: 'Hello and welcome.', categoryId: 1, categoryName: 'Greetings' }

/** The collections the page loads on mount. */
const loadRoutes = [
  { path: '/predefined-replies/categories', body: [category] },
  { path: '/predefined-replies', body: [reply] },
]

beforeEach(() => {
  // Only the debounce timers are faked; promise flushing keeps the real scheduler.
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('PredefinedRepliesView', () => {
  it('lists the categories', async () => {
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(PredefinedRepliesView)

    expect(wrapper.get('[data-testid="predefined-replies-category-1"]').text()).toContain('Greetings')
  })

  it('adds a category and clears the field', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-category-name"]').setValue(' Billing ')
    await wrapper.get('[data-testid="predefined-replies-category-add"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'POST')
    expect(post).toMatchObject({ url: '/api/predefined-replies/categories', body: { name: 'Billing' } })
    expect(wrapper.get('[data-testid="predefined-replies-feedback"]').text()).toBe('Record created.')
  })

  it('shows the API sentence when adding a category is refused', async () => {
    stubRoutes({
      'POST /api/predefined-replies/categories': () => problem('Name is taken.', 409),
      'GET /api/predefined-replies/categories': () => json([category]),
      'GET /api/predefined-replies': () => json([reply]),
    })
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-category-name"]').setValue('Greetings')
    await wrapper.get('[data-testid="predefined-replies-category-add"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="predefined-replies-feedback"]').text()).toBe('Name is taken.')
    expect(wrapper.find('[data-testid="predefined-replies-error"]').exists()).toBe(false)
  })

  it('lists the replies on the reply tab and blocks an incomplete new reply', async () => {
    stubPathRoutes(loadRoutes)
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-tab-reply"]').trigger('click')

    expect(wrapper.get('[data-testid="predefined-replies-reply-4"]').text()).toContain('Welcome')
    expect(wrapper.get('[data-testid="predefined-replies-reply-add"]').attributes('disabled')).toBeDefined()
  })

  it('opens the in-place edit form of a reply from its row actions menu', async () => {
    stubPathRoutes(loadRoutes)
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-tab-reply"]').trigger('click')
    // Edit and delete sit behind the row's own menu trigger.
    await wrapper.get('[data-testid="predefined-replies-reply-4-actions"]').trigger('click')
    await wrapper.get('[data-testid="predefined-replies-reply-4-edit"]').trigger('click')

    expect((wrapper.get('[data-testid="predefined-replies-reply-edit-name"]').element as HTMLInputElement).value).toBe('Welcome')
    expect(wrapper.find('[data-testid="predefined-replies-reply-4-actions"]').exists()).toBe(false)
  })

  it('searches once typing pauses and shows the hits', async () => {
    const mock = stubPathRoutes([...loadRoutes, { path: '/predefined-replies/search', body: [reply] }])
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-tab-search"]').trigger('click')
    await wrapper.get('[data-testid="predefined-replies-search"]').setValue('welc')
    await vi.advanceTimersByTimeAsync(400)
    await flushPromises()

    const urls = mock.mock.calls.map((_, index) => callOf(mock, index).url)
    expect(urls).toContain('/api/predefined-replies/search?q=welc')
    expect(wrapper.get('[data-testid="predefined-replies-result-4"]').text()).toContain('Hello and welcome.')
  })

  it('deletes a category after confirmation', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="predefined-replies-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    const removal = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'DELETE')
    expect(removal).toMatchObject({ url: '/api/predefined-replies/categories/1' })
    expect(wrapper.get('[data-testid="predefined-replies-feedback"]').text()).toBe('Record deleted.')
  })

  it('keeps the category when the delete is refused or dismissed', async () => {
    const mock = stubRoutes({
      'DELETE /api/predefined-replies/categories/1': () => problem('Category is in use.', 409),
      'GET /api/predefined-replies/categories': () => json([category]),
      'GET /api/predefined-replies': () => json([reply]),
    })
    const wrapper = await mountView(PredefinedRepliesView)

    await wrapper.get('[data-testid="predefined-replies-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="predefined-replies-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)

    await wrapper.get('[data-testid="predefined-replies-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="predefined-replies-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="predefined-replies-feedback"]').text()).toBe('Category is in use.')
    expect(wrapper.get('[data-testid="predefined-replies-category-1"]').text()).toContain('Greetings')
  })

  it('shows the API sentence when loading is refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const wrapper = await mountView(PredefinedRepliesView)

    expect(wrapper.get('[data-testid="predefined-replies-error"]').text()).toBe('Not allowed.')
  })

  it('loads again from the retry of a refused load', async () => {
    let refused = true
    stubRoutes({
      'GET /api/predefined-replies/categories': () => (refused ? problem('Not allowed.', 403) : json([category])),
      'GET /api/predefined-replies': () => (refused ? problem('Not allowed.', 403) : json([reply])),
    })
    const wrapper = await mountView(PredefinedRepliesView)
    refused = false

    await wrapper.get('[data-testid="predefined-replies-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="predefined-replies-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="predefined-replies-category-1"]').text()).toContain('Greetings')
  })
})
