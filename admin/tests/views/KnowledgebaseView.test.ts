/**
 * Tests for KnowledgebaseView: the category browser, adding a category, the article tab, the
 * delete confirmation, and the article form sending the Published checkbox to the API, through
 * the real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import KnowledgebaseView from '@/views/KnowledgebaseView.vue'
import { callsOf, findCall, json, problem, stubFetch } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'
import { pickOption } from '../helpers/select'

/** One category as the endpoint returns it. */
const category = { id: 1, name: 'Billing', description: 'Invoices and payments', isHidden: true, parentCategoryId: null, articleCount: 3, slug: 'billing' }

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

    const post = findCall(mock, 'POST', '/api/admin/knowledgebase/categories')
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

    const sent = callsOf(mock)
    expect(sent).toContainEqual(expect.objectContaining({ url: '/api/admin/knowledgebase/categories/1', method: 'DELETE' }))
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
    let refuse = true
    stubRoutes({
      'GET /api/admin/knowledgebase/categories': () => (refuse ? problem('Not allowed.', 403) : json([category])),
      'GET /api/admin/knowledgebase': () => json([]),
    })
    const wrapper = await mountView(KnowledgebaseView)

    refuse = false
    await wrapper.get('[data-testid="knowledgebase-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="knowledgebase-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="knowledgebase-category-1"]').exists()).toBe(true)
  })
})

/** One category the article form can file under. */
const CATEGORIES = [{ id: 3, name: 'Billing', description: '', isHidden: false, parentCategoryId: null, articleCount: 0, slug: 'billing' }]

/** One stored article, written in English and Russian and published. */
const STORED_ARTICLE = {
  id: 11,
  slug: 'how-to-pay',
  categoryId: 3,
  categoryName: 'Billing',
  isPublished: true,
  translations: { en: { title: 'How to pay', content: 'Use a card' }, ru: { title: 'Как оплатить', content: 'Картой' } },
}

/**
 * Mounts the view on the article tab with the categories and the given stored articles.
 *
 * @param articles - The stored articles the list returns.
 * @returns The wrapper and the fetch mock.
 */
const mountArticles = async (articles: unknown[] = []) => {
  const mock = stubRoutes({
    'GET /api/admin/knowledgebase/categories': () => json(CATEGORIES),
    'GET /api/admin/knowledgebase': () => json(articles),
    'POST /api/admin/knowledgebase': () => json(11, 201),
    'PUT /api/admin/knowledgebase/11': () => new Response(null, { status: 204 }),
  })
  const wrapper = await mountView(KnowledgebaseView)
  await wrapper.get('[data-testid="knowledgebase-tab-article"]').trigger('click')
  return { wrapper, mock }
}

/**
 * Fills the English tab of the article form and picks the category.
 *
 * @param wrapper - The mounted view.
 * @returns Resolves once the title, the body and the category are set.
 */
const fillEnglish = async (wrapper: Awaited<ReturnType<typeof mountArticles>>['wrapper']): Promise<void> => {
  await wrapper.get('[data-testid="knowledgebase-article-title"]').setValue('How to pay')
  await wrapper.get('[data-testid="knowledgebase-article-content"]').setValue('Use a card')
  await pickOption(wrapper, 'knowledgebase-article-category', 'Billing')
}

describe('KnowledgebaseView article form', () => {
  it('sends the category id and the English text, published when the box is ticked', async () => {
    const { wrapper, mock } = await mountArticles()
    await fillEnglish(wrapper)
    await wrapper.get('[data-testid="knowledgebase-article-published-label"]').trigger('click')

    await wrapper.get('[data-testid="knowledgebase-article-save"]').trigger('click')
    await flushPromises()

    expect(findCall(mock, 'POST', '/api/admin/knowledgebase')?.body).toEqual({
      categoryId: 3,
      translations: { en: { title: 'How to pay', content: 'Use a card' } },
      isPublished: true,
    })
  })

  it('sends a draft when the box is not ticked', async () => {
    const { wrapper, mock } = await mountArticles()
    await fillEnglish(wrapper)

    await wrapper.get('[data-testid="knowledgebase-article-save"]').trigger('click')
    await flushPromises()

    expect(findCall(mock, 'POST', '/api/admin/knowledgebase')?.body).toMatchObject({ isPublished: false })
  })

  it('carries the text of each language in its own tab and sends every language that has text', async () => {
    const { wrapper, mock } = await mountArticles()
    await fillEnglish(wrapper)

    await wrapper.get('[data-testid="knowledgebase-article-locale-ru"]').trigger('click')
    expect((wrapper.get('[data-testid="knowledgebase-article-title"]').element as HTMLInputElement).value).toBe('')
    await wrapper.get('[data-testid="knowledgebase-article-title"]').setValue('Как оплатить')
    await wrapper.get('[data-testid="knowledgebase-article-content"]').setValue('Картой')
    await wrapper.get('[data-testid="knowledgebase-article-locale-en"]').trigger('click')
    expect((wrapper.get('[data-testid="knowledgebase-article-title"]').element as HTMLInputElement).value).toBe('How to pay')

    await wrapper.get('[data-testid="knowledgebase-article-save"]').trigger('click')
    await flushPromises()

    expect(findCall(mock, 'POST', '/api/admin/knowledgebase')?.body).toMatchObject({
      translations: { en: { title: 'How to pay', content: 'Use a card' }, ru: { title: 'Как оплатить', content: 'Картой' } },
    })
  })

  it('cannot be saved without the English text, even with another language filled', async () => {
    const { wrapper } = await mountArticles()
    await pickOption(wrapper, 'knowledgebase-article-category', 'Billing')
    await wrapper.get('[data-testid="knowledgebase-article-locale-ru"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-article-title"]').setValue('Только русский')
    await wrapper.get('[data-testid="knowledgebase-article-content"]').setValue('Текст')

    expect(wrapper.get('[data-testid="knowledgebase-article-save"]').attributes('disabled')).toBeDefined()
  })

  it('shows the sentence of a refused save and keeps the form', async () => {
    const mock = stubRoutes({
      'GET /api/admin/knowledgebase/categories': () => json(CATEGORIES),
      'GET /api/admin/knowledgebase': () => json([]),
      'POST /api/admin/knowledgebase': () => problem('Check the form.', 400),
    })
    const wrapper = await mountView(KnowledgebaseView)
    await wrapper.get('[data-testid="knowledgebase-tab-article"]').trigger('click')
    await fillEnglish(wrapper)

    await wrapper.get('[data-testid="knowledgebase-article-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="knowledgebase-feedback"]').text()).toBe('Check the form.')
    expect((wrapper.get('[data-testid="knowledgebase-article-title"]').element as HTMLInputElement).value).toBe('How to pay')
    expect(mock).toHaveBeenCalled()
  })
})

describe('KnowledgebaseView article list loading', () => {
  it('shows an error with Retry when the articles are refused, not the empty state, and reloads them', async () => {
    let refuse = true
    stubRoutes({
      'GET /api/admin/knowledgebase/categories': () => json(CATEGORIES),
      'GET /api/admin/knowledgebase': () => (refuse ? problem('Not allowed.', 403) : json([STORED_ARTICLE])),
    })
    const wrapper = await mountView(KnowledgebaseView)
    await wrapper.get('[data-testid="knowledgebase-tab-article"]').trigger('click')

    expect(wrapper.get('[data-testid="knowledgebase-articles-error"]').text()).toContain('Not allowed.')
    expect(wrapper.find('[data-testid="knowledgebase-articles-empty"]').exists()).toBe(false)

    refuse = false
    await wrapper.get('[data-testid="knowledgebase-articles-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="knowledgebase-articles-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="knowledgebase-article-11"]').exists()).toBe(true)
  })

  it('keeps the category list when only the articles are refused', async () => {
    stubRoutes({
      'GET /api/admin/knowledgebase/categories': () => json(CATEGORIES),
      'GET /api/admin/knowledgebase': () => problem('Not allowed.', 403),
    })

    const wrapper = await mountView(KnowledgebaseView)

    expect(wrapper.find('[data-testid="knowledgebase-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="knowledgebase-category-3"]').exists()).toBe(true)
  })

  it('labels the save button Save Article for a new article and Save Changes while editing', async () => {
    const { wrapper } = await mountArticles([STORED_ARTICLE])
    expect(wrapper.get('[data-testid="knowledgebase-article-save"]').text()).toBe('Save Article')

    await wrapper.get('[data-testid="knowledgebase-article-11-edit"]').trigger('click')

    expect(wrapper.get('[data-testid="knowledgebase-article-save"]').text()).toBe('Save Changes')
  })
})

describe('KnowledgebaseView article list and editing', () => {
  it('lists the articles with their title, address, category and status', async () => {
    const { wrapper } = await mountArticles([STORED_ARTICLE, { ...STORED_ARTICLE, id: 12, slug: 'draft', isPublished: false }])

    const row = wrapper.get('[data-testid="knowledgebase-article-11"]')

    expect(row.text()).toContain('How to pay')
    expect(row.text()).toContain('how-to-pay')
    expect(row.text()).toContain('Billing')
    expect(row.text()).toContain('Published')
    expect(wrapper.get('[data-testid="knowledgebase-article-12"]').text()).toContain('Draft')
  })

  it('shows the first language an article has when it has no English text', async () => {
    const { wrapper } = await mountArticles([{ ...STORED_ARTICLE, translations: { hy: { title: 'Վերնագիր', content: 'Տեքստ' } } }])

    expect(wrapper.get('[data-testid="knowledgebase-article-11"]').text()).toContain('Վերնագիր')
  })

  it('shows an empty state when there are no articles', async () => {
    const { wrapper } = await mountArticles([])

    expect(wrapper.find('[data-testid="knowledgebase-articles-empty"]').exists()).toBe(true)
  })

  it('loads an article into the form from its Edit action, without the published box', async () => {
    const { wrapper } = await mountArticles([STORED_ARTICLE])

    await wrapper.get('[data-testid="knowledgebase-article-11-edit"]').trigger('click')

    expect((wrapper.get('[data-testid="knowledgebase-article-title"]').element as HTMLInputElement).value).toBe('How to pay')
    await wrapper.get('[data-testid="knowledgebase-article-locale-ru"]').trigger('click')
    expect((wrapper.get('[data-testid="knowledgebase-article-title"]').element as HTMLInputElement).value).toBe('Как оплатить')
    expect(wrapper.find('[data-testid="knowledgebase-article-published"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="knowledgebase-article-cancel"]').exists()).toBe(true)
  })

  it('saves an edit with a PUT of the category and every language, then reloads the list', async () => {
    const { wrapper, mock } = await mountArticles([STORED_ARTICLE])
    await wrapper.get('[data-testid="knowledgebase-article-11-edit"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-article-locale-hy"]').trigger('click')
    await wrapper.get('[data-testid="knowledgebase-article-title"]').setValue('Ինչպես վճարել')
    await wrapper.get('[data-testid="knowledgebase-article-content"]').setValue('Քարտով')

    await wrapper.get('[data-testid="knowledgebase-article-save"]').trigger('click')
    await flushPromises()

    expect(findCall(mock, 'PUT', '/api/admin/knowledgebase/11')?.body).toEqual({
      categoryId: 3,
      translations: {
        en: { title: 'How to pay', content: 'Use a card' },
        ru: { title: 'Как оплатить', content: 'Картой' },
        hy: { title: 'Ինչպես վճարել', content: 'Քարտով' },
      },
    })
    expect(wrapper.get('[data-testid="knowledgebase-feedback"]').text()).toBe('Article saved.')
    expect(wrapper.find('[data-testid="knowledgebase-article-cancel"]').exists()).toBe(false)
  })

  it('cancel leaves edit mode and empties the form', async () => {
    const { wrapper } = await mountArticles([STORED_ARTICLE])
    await wrapper.get('[data-testid="knowledgebase-article-11-edit"]').trigger('click')

    await wrapper.get('[data-testid="knowledgebase-article-cancel"]').trigger('click')

    expect((wrapper.get('[data-testid="knowledgebase-article-title"]').element as HTMLInputElement).value).toBe('')
    expect(wrapper.find('[data-testid="knowledgebase-article-published"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="knowledgebase-article-save"]').attributes('disabled')).toBeDefined()
  })
})
