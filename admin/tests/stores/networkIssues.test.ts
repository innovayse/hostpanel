/**
 * Tests for the network issues store: loads fill state, a refusal puts the API's sentence into
 * `error`, an unreachable API puts the shared no-answer sentence, and a refused save exposes the
 * field messages. The requests go through the real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useNetworkIssuesStore } from '@/stores/networkIssues'
import { json, problem, stubFetch, stubOffline, callOf, invalid } from '../helpers/fetch'
import { NetworkIssuePriority, NetworkIssueStatus, NetworkIssueType } from '@/types/networkissue'

/** A valid network issue body. */
const payload = {
  title: 'Packet loss',
  type: NetworkIssueType.Server,
  server: null,
  priority: NetworkIssuePriority.High,
  status: NetworkIssueStatus.Reported,
  startDate: '2026-10-14T08:00:00Z',
  endDate: null,
  description: 'Investigating',
}

describe('networkIssuesStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll requests the page given, with no filter, and fills the list', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], totalCount: 1 }))
    const store = useNetworkIssuesStore()

    expect(await store.fetchAll(1)).toBe(true)
    expect(callOf(mock).url).toBe('/api/network-issues?page=1&pageSize=20')
    expect(store.issues).toEqual([{ id: 1 }])
    expect(store.totalCount).toBe(1)
  })

  it('fetchAll sends the trimmed search and the status, type and priority filters', async () => {
    const mock = stubFetch(json({ items: [], totalCount: 0 }))
    const store = useNetworkIssuesStore()

    await store.fetchAll(2, {
      search: '  loss  ',
      status: NetworkIssueStatus.Resolved,
      type: NetworkIssueType.Other,
      priority: NetworkIssuePriority.High,
    })

    expect(callOf(mock).url).toBe('/api/network-issues?page=2&pageSize=20&search=loss&status=Resolved&type=Other&priority=High')
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useNetworkIssuesStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll(1)).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
  })

  it('fetchById stores the issue and clears it on refusal', async () => {
    const store = useNetworkIssuesStore()
    stubFetch(json({ id: 2 }))
    expect(await store.fetchById(2)).toBe(true)
    expect(store.current).toEqual({ id: 2 })

    stubFetch(problem('No such issue.', 404))
    expect(await store.fetchById(3)).toBe(false)
    expect(store.current).toBeNull()
  })

  it('create returns the id, null with field messages on refusal', async () => {
    const store = useNetworkIssuesStore()
    stubFetch(json(5))
    expect(await store.create(payload)).toBe(5)

    stubFetch(invalid('Check the form.', { Title: ['Title is required.'] }))
    expect(await store.create({ ...payload, title: '' })).toBeNull()
    expect(store.fieldErrors).toEqual({ title: 'Title is required.' })
  })

  it('update and remove report the outcome', async () => {
    const store = useNetworkIssuesStore()
    const mock = stubFetch(new Response(null, { status: 204 }))

    expect(await store.update(2, payload)).toBe(true)
    expect(await store.remove(2)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/network-issues/2', method: 'PUT', body: payload })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/network-issues/2', method: 'DELETE' })

    stubFetch(problem('Locked.', 409))
    expect(await store.update(2, payload)).toBe(false)
    expect(store.error).toBe('Locked.')
  })

  it('keeps a refused delete apart from the load error', async () => {
    const store = useNetworkIssuesStore()
    stubFetch(problem('In use.', 409))

    expect(await store.remove(2)).toBe(false)
    expect(store.actionError).toBe('In use.')
    expect(store.error).toBeNull()
  })
})
