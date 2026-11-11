/**
 * Tests for the knowledge base store: loads fill state, category writes reload the list, a
 * refusal puts the API's sentence into `error`, and a refused save exposes the field messages.
 * The requests go through the real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useKnowledgebaseStore } from '@/stores/knowledgebase'
import { json, problem, stubFetch, stubOffline, callOf, invalid } from '../helpers/fetch'

/** A valid category body. */
const category = { name: 'Billing', description: 'Invoices', isHidden: false }

/** A valid article body: a category id, one English translation, published. */
const article = { categoryId: 3, translations: { en: { title: 'How to pay', content: 'Use a card' } }, isPublished: true }

describe('knowledgebaseStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchCategories and fetchArticles fill their lists', async () => {
    const store = useKnowledgebaseStore()
    stubFetch(json([{ id: 1 }]))
    expect(await store.fetchCategories()).toBe(true)
    expect(store.categories).toEqual([{ id: 1 }])

    stubFetch(json([{ id: 2 }]))
    expect(await store.fetchArticles()).toBe(true)
    expect(store.articles).toEqual([{ id: 2 }])
    expect(store.loading).toBe(false)
    expect(store.articlesLoading).toBe(false)
  })

  it('a refused load reports the API sentence, the no-answer sentence offline', async () => {
    const store = useKnowledgebaseStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchArticles()).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchCategories()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
  })

  it('createCategory returns the id and reloads the categories', async () => {
    const store = useKnowledgebaseStore()
    const mock = stubFetch(json(5), json([{ id: 5, name: 'Billing' }]))

    expect(await store.createCategory(category)).toBe(5)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/knowledgebase/categories', method: 'POST', body: category })
    expect(callOf(mock, 1).url).toBe('/api/admin/knowledgebase/categories')
    expect(store.categories).toEqual([{ id: 5, name: 'Billing' }])
  })

  it('createCategory returns null with the field messages and does not reload', async () => {
    const store = useKnowledgebaseStore()
    const mock = stubFetch(invalid('Check the form.', { Name: ['Name is required.'] }))

    expect(await store.createCategory({ ...category, name: '' })).toBeNull()
    expect(store.fieldErrors).toEqual({ name: 'Name is required.' })
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('updateCategory and deleteCategory reload on success only', async () => {
    const store = useKnowledgebaseStore()
    const mock = stubFetch(json({}), json([]))
    expect(await store.updateCategory(5, category)).toBe(true)
    expect(await store.deleteCategory(5)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/knowledgebase/categories/5', method: 'PUT' })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/admin/knowledgebase/categories/5', method: 'DELETE' })

    const refused = stubFetch(problem('Category is in use.', 409))
    expect(await store.deleteCategory(5)).toBe(false)
    expect(store.error).toBe('Category is in use.')
    expect(refused).toHaveBeenCalledTimes(1)
  })

  it('createArticle sends the category id and translations, returns the id and reloads the articles', async () => {
    const store = useKnowledgebaseStore()
    const mock = stubFetch(json(11), json([{ id: 11, slug: 'how-to-pay' }]))

    expect(await store.createArticle(article)).toBe(11)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/knowledgebase', method: 'POST', body: article })
    expect(callOf(mock, 1).url).toBe('/api/admin/knowledgebase')
    expect(store.articles).toEqual([{ id: 11, slug: 'how-to-pay' }])
  })

  it('createArticle returns null with the field messages and does not reload', async () => {
    const store = useKnowledgebaseStore()
    const mock = stubFetch(invalid('Check the form.', { Translations: ['English is required.'] }))

    expect(await store.createArticle({ ...article, translations: {} })).toBeNull()
    expect(store.fieldErrors).toEqual({ translations: 'English is required.' })
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('updateArticle puts the category and translations and reloads on success only', async () => {
    const store = useKnowledgebaseStore()
    const update = { categoryId: article.categoryId, translations: article.translations }
    const mock = stubFetch(json({}), json([{ id: 11 }]))

    expect(await store.updateArticle(11, update)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/knowledgebase/11', method: 'PUT', body: update })
    expect(store.articles).toEqual([{ id: 11 }])

    const refused = stubFetch(problem('No such category.', 404))
    expect(await store.updateArticle(11, update)).toBe(false)
    expect(store.error).toBe('No such category.')
    expect(refused).toHaveBeenCalledTimes(1)
  })

  it('deleteArticle reports the outcome', async () => {
    const store = useKnowledgebaseStore()

    stubFetch(problem('No such article.', 404))
    expect(await store.deleteArticle(11)).toBe(false)
    expect(store.error).toBe('No such article.')
  })

  it('the articles have their own loading flag: a slow articles load does not mark the categories as loading', async () => {
    const store = useKnowledgebaseStore()
    let release: () => void = () => undefined
    const gate = new Promise<void>((resolve) => {
      release = resolve
    })
    vi.stubGlobal('fetch', vi.fn(() => gate.then(() => json([{ id: 2 }]))))

    const pending = store.fetchArticles()

    expect(store.articlesLoading).toBe(true)
    expect(store.loading).toBe(false)
    release()
    await pending
    expect(store.articlesLoading).toBe(false)
  })
})
