/**
 * Tests for the predefined replies API composable: paths, verbs, query strings and bodies
 * through the real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { usePredefinedRepliesApi } from '@/composables/apis/usePredefinedRepliesApi'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A valid reply body. */
const reply = { name: 'Greeting', content: 'Hello', categoryId: 2 }

describe('usePredefinedRepliesApi', () => {
  it('category calls carry the name as the body', async () => {
    const mock = stubFetch(json(6))
    const api = usePredefinedRepliesApi()

    expect(await api.createCategory('General')).toBe(6)
    await api.updateCategory(6, 'Renamed')
    await api.deleteCategory(6)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/predefined-replies/categories', method: 'POST', body: { name: 'General' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/predefined-replies/categories/6', method: 'PUT', body: { name: 'Renamed' } })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/predefined-replies/categories/6', method: 'DELETE' })
  })

  it('listReplies filters by category only when one is given', async () => {
    const mock = stubFetch(json([]))
    const api = usePredefinedRepliesApi()

    await api.listReplies(2)
    await api.listReplies()
    expect(callOf(mock, 0).url).toBe('/api/predefined-replies?categoryId=2')
    expect(callOf(mock, 1).url).toBe('/api/predefined-replies')
  })

  it('searchReplies encodes the query', async () => {
    const mock = stubFetch(json([]))

    await usePredefinedRepliesApi().searchReplies('a b&c')
    expect(callOf(mock).url).toBe('/api/predefined-replies/search?search=a+b%26c')
  })

  it('reply writes hit /predefined-replies', async () => {
    const mock = stubFetch(json(3))
    const api = usePredefinedRepliesApi()

    expect(await api.createReply(reply)).toBe(3)
    await api.updateReply(3, reply)
    await api.deleteReply(3)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/predefined-replies', method: 'POST', body: reply })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/predefined-replies/3', method: 'PUT', body: reply })
    expect(callOf(mock, 2)).toMatchObject({ url: '/api/predefined-replies/3', method: 'DELETE' })
  })
})
