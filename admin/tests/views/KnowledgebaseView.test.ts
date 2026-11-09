/**
 * Tests for KnowledgebaseView: the category browser, adding a category, the article tab, the
 * delete confirmation, and the article form sending the Published checkbox to the API, through
 * the real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import KnowledgebaseView from '@/views/KnowledgebaseView.vue'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'
import { pickOption } from '../helpers/select'

/** One category as the endpoint returns it. */
const category = { id: 1, name: 'Billing', description: 'Invoices and payments', isHidden: true, parentCategoryId: null, articleCount: 3 }

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('KnowledgebaseView', () => {
  it('lists the categories with their article count and hidden badge', async () => {
    stubPathRoutes([{ path: '/admin/knowledgebase/categories', body: [category] }])

    const wrapper = await mountView(KnowledgebaseView)
    const card = wrapper.get('[data-testid="knowledgebase-category-1"]')

    expect(card.text()).toContain('Billing')
    expect(card.text()).toContain('(3)')
    expect(card.text()).toContain('Hidden')
  })

  it('opens the in-place edit form from the row actions menu', async () => {
    stubPathRoutes([{ path: '/admin/knowledgebase/categories', body: [category] }])
    const wrapper = await mountView(KnowledgebaseView)

    // Edit and delete sit behind the row's own menu trigger.
    await wrapper.get('[data-testid="knowledgebase-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-category-1-edit"]').trigger('click')

    expect((wrapper.get('[data-testid="knowledgebase-edit-name"]').element as HTMLInputElement).value).toBe('Billing')
    expect(wrapper.find('[data-testid="knowledgebase-category-1-actions"]').exists()).toBe(false)
  })

  it('adds a category from the form', async () => {
    const mock = stubPathRoutes([{ path: '/admin/knowledgebase/categories', body: [] }])
    const wrapper = await mountView(KnowledgebaseView)
    expect(wrapper.get('[data-testid="knowledgebase-category-add"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="knowledgebase-category-name"]').setValue('  Hosting ')
    await wrapper.get('[data-testid="knowledgebase-category-add"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'POST')
    expect(post).toMatchObject({ url: '/api/admin/knowledgebase/categories', body: { name: 'Hosting', description: '', isHidden: false } })
  })

  it('switches to the article form, which cannot be saved empty', async () => {
    stubPathRoutes([{ path: '/admin/knowledgebase/categories', body: [category] }])
    const wrapper = await mountView(KnowledgebaseView)

    await wrapper.get('[data-testid="knowledgebase-tab-article"]').trigger('click')

    expect(wrapper.find('[data-testid="knowledgebase-category-add"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="knowledgebase-article-title"]').attributes('placeholder')).toBeTruthy()
    expect(wrapper.get('[data-testid="knowledgebase-article-save"]').attributes('disabled')).toBeDefined()
  })

  it('deletes a category after confirmation', async () => {
    const mock = stubPathRoutes([{ path: '/admin/knowledgebase/categories', body: [category] }])
    const wrapper = await mountView(KnowledgebaseView)

    await wrapper.get('[data-testid="knowledgebase-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/knowledgebase/categories/1', method: 'DELETE' })
  })

  it('shows the API sentence when the categories are refused', async () => {
    stubFetch(problem('Not allowed.', 403))

    const wrapper = await mountView(KnowledgebaseView)

    expect(wrapper.get('[data-testid="knowledgebase-error"]').text()).toBe('Not allowed.')
  })

  /**
   * Stubs the category list and the answer of the category's delete.
   *
   * @param answer - The answer of `DELETE /api/admin/knowledgebase/categories/1`.
   * @returns The fetch mock.
   */
  const stubDelete = (answer: () => Response): ReturnType<typeof vi.fn> =>
    stubRoutes({
      'DELETE /api/admin/knowledgebase/categories/1': answer,
      'GET /api/admin/knowledgebase/categories': () => json([category]),
    })

  it('reports a deleted category under the header', async () => {
    stubDelete(() => new Response(null, { status: 204 }))
    const wrapper = await mountView(KnowledgebaseView)

    await wrapper.get('[data-testid="knowledgebase-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="knowledgebase-feedback"]').text()).toBe('Category deleted.')
  })

  it('shows the API sentence when the delete is refused, without the load error', async () => {
    stubDelete(() => problem('The category still has articles.', 409))
    const wrapper = await mountView(KnowledgebaseView)

    await wrapper.get('[data-testid="knowledgebase-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="knowledgebase-feedback"]').text()).toBe('The category still has articles.')
    expect(wrapper.find('[data-testid="knowledgebase-error"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubDelete(() => new Response(null, { status: 204 }))
    const wrapper = await mountView(KnowledgebaseView)

    await wrapper.get('[data-testid="knowledgebase-category-1-actions"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-category-1-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads the categories from the Retry of a refused load', async () => {
    stubFetch(problem('Not allowed.', 403), json([category]))
    const wrapper = await mountView(KnowledgebaseView)

    await wrapper.get('[data-testid="knowledgebase-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="knowledgebase-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="knowledgebase-category-1"]').exists()).toBe(true)
  })
})

/** One category the article form can file under. */
const CATEGORIES = [{ id: 3, name: 'Billing', description: '', isHidden: false, parentCategoryId: null, articleCount: 0 }]

/**
 * Mounts the view with a category list and fills the article form.
 *
 * @param publish - Whether to tick the Published checkbox.
 * @returns The sent article body.
 */
const submitArticle = async (publish: boolean): Promise<unknown> => {
  const mock = stubRoutes({
    'GET /api/admin/knowledgebase/categories': () => json(CATEGORIES),
    'POST /api/admin/knowledgebase': () => json(11, 201),
  })
  const wrapper = await mountView(KnowledgebaseView)
  await wrapper.get('[data-testid="knowledgebase-tab-article"]').trigger('click')
  await wrapper.get('[data-testid="knowledgebase-article-title"]').setValue('How to pay')
  await wrapper.get('[data-testid="knowledgebase-article-content"]').setValue('Use a card')
  await pickOption(wrapper, 'knowledgebase-article-category', 'Billing')
  if (publish) {
    await wrapper.get('[data-testid="knowledgebase-article-published-label"]').trigger('click')
  }
  await wrapper.get('[data-testid="knowledgebase-article-save"]').trigger('click')
  const post = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'POST' && String(init && (init as RequestInit).body).includes('How to pay'))
  const init = post?.[1] as RequestInit | undefined
  return JSON.parse(String(init?.body))
}

describe('KnowledgebaseView article publishing', () => {
  it('sends isPublished true when the Published box is ticked', async () => {
    expect(await submitArticle(true)).toMatchObject({ title: 'How to pay', category: 'Billing', isPublished: true })
  })

  it('sends isPublished false for a draft', async () => {
    expect(await submitArticle(false)).toMatchObject({ isPublished: false })
  })
})
