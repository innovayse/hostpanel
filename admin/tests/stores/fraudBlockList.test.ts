/**
 * Tests for the fraud block list store: a read fills the page and the total, an add and a delete
 * report whether the API took them, and a refusal lands in `actionError` (with its per-field
 * messages for an add) until it is cleared.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useFraudBlockListStore } from '@/stores/fraudBlockList'
import { BlockListType } from '@/types/fraud'
import type { BlockListEntry } from '@/types/fraud'
import { callOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

/** Sentence the client writes when nothing answered. */
const NO_ANSWER = 'Could not reach the server. Check your connection and try again.'

/** One entry as the list endpoint returns it. */
const entry: BlockListEntry = {
  id: 4,
  type: BlockListType.Ip,
  value: '203.0.113.7',
  note: 'spam',
  createdAt: '2026-11-01T10:00:00Z',
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('fraudBlockListStore', () => {
  it('fetchPage fills the entries and the total and clears loading', async () => {
    const mock = stubFetch(json({ items: [entry], pageNumber: 1, pageSize: 20, totalCount: 5, totalPages: 1 }))
    const store = useFraudBlockListStore()

    await store.fetchPage(BlockListType.Ip, 1, 20)

    expect(store.entries).toEqual([entry])
    expect(store.totalCount).toBe(5)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
    expect(callOf(mock).url).toBe('/api/admin/fraud/blocklist?page=1&pageSize=20&type=Ip')
  })

  it('fetchPage shows the API sentence on refusal and keeps the earlier page', async () => {
    const store = useFraudBlockListStore()
    stubFetch(json({ items: [entry], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }))
    await store.fetchPage(BlockListType.Ip, 1, 20)

    stubFetch(problem('Not allowed.', 403))
    await store.fetchPage(BlockListType.Ip, 1, 20)

    expect(store.error).toBe('Not allowed.')
    expect(store.entries).toEqual([entry])
    expect(store.loading).toBe(false)
  })

  it('fetchPage shows the no-answer sentence when the network is down', async () => {
    stubOffline()
    const store = useFraudBlockListStore()

    await store.fetchPage(BlockListType.Ip, 1, 20)

    expect(store.error).toBe(NO_ANSWER)
  })

  it('add returns true when the API creates the entry', async () => {
    const mock = stubFetch(json(8, 201))
    const store = useFraudBlockListStore()

    const added = await store.add({ type: BlockListType.Country, value: 'FR', note: null })

    expect(added).toBe(true)
    expect(store.actionError).toBeNull()
    expect(callOf(mock).body).toEqual({ type: 'Country', value: 'FR', note: null })
  })

  it('add keeps the refusal sentence and the field messages', async () => {
    stubFetch(invalid('The value is not valid.', { Value: ['Enter a valid IP address.'] }))
    const store = useFraudBlockListStore()

    const added = await store.add({ type: BlockListType.Ip, value: 'nope', note: null })

    expect(added).toBe(false)
    expect(store.actionError).toBe('The value is not valid.')
    expect(store.fieldErrors).toEqual({ value: 'Enter a valid IP address.' })
  })

  it('clearAction forgets the refusal of the last add', async () => {
    stubFetch(invalid('The value is not valid.', { Value: ['Enter a valid IP address.'] }))
    const store = useFraudBlockListStore()
    await store.add({ type: BlockListType.Ip, value: 'nope', note: null })

    store.clearAction()

    expect(store.actionError).toBeNull()
    expect(store.fieldErrors).toEqual({})
  })

  it('remove returns true when the API deletes the entry', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const store = useFraudBlockListStore()

    const removed = await store.remove(4)

    expect(removed).toBe(true)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/fraud/blocklist/4', method: 'DELETE' })
  })

  it('remove keeps the refusal sentence when the entry is gone', async () => {
    stubFetch(problem('The entry does not exist.', 404))
    const store = useFraudBlockListStore()

    const removed = await store.remove(4)

    expect(removed).toBe(false)
    expect(store.actionError).toBe('The entry does not exist.')
  })
})
