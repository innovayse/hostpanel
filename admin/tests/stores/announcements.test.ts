/**
 * Tests for the announcements store: loads fill state, a refusal puts the API's sentence into
 * `error`, an unreachable API puts the shared no-answer sentence, and a refused save exposes the
 * field messages. The requests go through the real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useAnnouncementsStore } from '@/stores/announcements'
import { json, problem, stubFetch, stubOffline, callOf, invalid } from '../helpers/fetch'

/** A valid announcement body. */
const payload = { title: 'Maintenance', content: '<p>Tonight</p>', isPublished: true }

describe('announcementsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll requests the page given and fills the list', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 1 }))
    const store = useAnnouncementsStore()

    expect(await store.fetchAll(2)).toBe(true)
    expect(callOf(mock).url).toBe('/api/announcements?page=2&pageSize=20')
    expect(store.announcements).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(1)
  })

  it('fetchAll sends the trimmed search and the published state', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useAnnouncementsStore()

    await store.fetchAll(1, { search: '  window  ', isPublished: false })

    expect(callOf(mock).url).toBe('/api/announcements?page=1&pageSize=20&search=window&isPublished=false')
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useAnnouncementsStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
  })

  it('fetchById stores the announcement and clears it on refusal', async () => {
    const store = useAnnouncementsStore()
    stubFetch(json({ id: 3 }))
    expect(await store.fetchById(3)).toBe(true)
    expect(store.current).toEqual({ id: 3 })

    stubFetch(problem('No such announcement.', 404))
    expect(await store.fetchById(4)).toBe(false)
    expect(store.current).toBeNull()
    expect(store.error).toBe('No such announcement.')
  })

  it('create returns the id, null with field messages on refusal', async () => {
    const store = useAnnouncementsStore()
    stubFetch(json(9))
    expect(await store.create(payload)).toBe(9)

    stubFetch(invalid('Check the form.', { Title: ['Title is required.'] }))
    expect(await store.create({ ...payload, title: '' })).toBeNull()
    expect(store.fieldErrors).toEqual({ title: 'Title is required.' })
    expect(store.loading).toBe(false)
  })

  it('update and remove report the outcome', async () => {
    const store = useAnnouncementsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))

    expect(await store.update(3, payload)).toBe(true)
    expect(await store.remove(3)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/announcements/3', method: 'PUT', body: payload })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/announcements/3', method: 'DELETE' })

    stubFetch(problem('Locked.', 409))
    expect(await store.remove(3)).toBe(false)
    expect(store.error).toBe('Locked.')
  })
})
