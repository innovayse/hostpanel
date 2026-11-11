/**
 * Tests for the knowledge base API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useKnowledgebaseApi } from '@/composables/apis/useKnowledgebaseApi'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A valid category body. */
const category = { name: 'Billing', description: 'Invoices', isHidden: false }

describe('useKnowledgebaseApi', () => {
  it('listCategories and listArticles read the admin paths', async () => {
    const mock = stubFetch(json([]))
    const api = useKnowledgebaseApi()

    await api.listCategories()
    await api.listArticles()
    expect(callOf(mock, 0).url).toBe('/api/admin/knowledgebase/categories')
    expect(callOf(mock, 1).url).toBe('/api/admin/knowledgebase')
  })

  it('createCategory POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(5))

    expect(await useKnowledgebaseApi().createCategory(category)).toBe(5)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/knowledgebase/categories', method: 'POST', body: category })
  })

  it('updateCategory PUTs and deleteCategory DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useKnowledgebaseApi()

    await api.updateCategory(5, category)
    await api.deleteCategory(5)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/knowledgebase/categories/5', method: 'PUT', body: category })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/knowledgebase/categories/5', method: 'DELETE' })
  })

  it('createArticle POSTs the category id and translations and returns the id', async () => {
    const mock = stubFetch(json(11))
    const article = { categoryId: 3, translations: { en: { title: 'How to pay', content: 'Use a card' } }, isPublished: true }

    expect(await useKnowledgebaseApi().createArticle(article)).toBe(11)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/knowledgebase', method: 'POST', body: article })
  })

  it('updateArticle PUTs the category id and translations, deleteArticle DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useKnowledgebaseApi()
    const update = { categoryId: 3, translations: { en: { title: 'Pay', content: 'Card' }, ru: { title: 'Оплата', content: 'Картой' } } }

    await api.updateArticle(11, update)
    await api.deleteArticle(11)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/knowledgebase/11', method: 'PUT', body: update })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/knowledgebase/11', method: 'DELETE' })
  })
})
