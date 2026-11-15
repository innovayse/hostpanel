/**
 * Tests for the announcements API composable: paths, verbs, query strings and bodies through the
 * real `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useAnnouncementsApi } from '@/composables/apis/useAnnouncementsApi'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A valid announcement body. */
const payload = { title: 'Maintenance', content: '<p>Tonight</p>', isPublished: true }

describe('useAnnouncementsApi', () => {
  it('listAnnouncements sends the page and size', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useAnnouncementsApi().listAnnouncements({ page: 3, pageSize: 10 })
    expect(callOf(mock)).toMatchObject({ url: '/api/announcements?page=3&pageSize=10', method: 'GET' })
  })

  it('listAnnouncements sends the search and the published state that are set', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))

    await useAnnouncementsApi().listAnnouncements({ page: 1, pageSize: 10, search: 'window', isPublished: true })
    expect(callOf(mock).url).toBe('/api/announcements?page=1&pageSize=10&search=window&isPublished=true')
  })

  it('getAnnouncement reads one announcement', async () => {
    const mock = stubFetch(json({ id: 4 }))

    expect(await useAnnouncementsApi().getAnnouncement(4)).toEqual({ id: 4 })
    expect(callOf(mock).url).toBe('/api/announcements/4')
  })

  it('createAnnouncement POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(12))

    expect(await useAnnouncementsApi().createAnnouncement(payload)).toBe(12)
    expect(callOf(mock)).toMatchObject({ url: '/api/announcements', method: 'POST', body: payload })
  })

  it('updateAnnouncement PUTs and deleteAnnouncement DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))
    const api = useAnnouncementsApi()

    await api.updateAnnouncement(4, payload)
    await api.deleteAnnouncement(4)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/announcements/4', method: 'PUT', body: payload })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/announcements/4', method: 'DELETE' })
  })
})
