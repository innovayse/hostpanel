/**
 * Tests for `composables/apis/useContentApi.ts`.
 *
 * Each endpoint function is called for real through `apiFetch` against a stub server, and the
 * test asserts on the path, verb, query and body that arrived and on how the reply was typed.
 *
 * @module tests/composables/apis/useContentApi.test
 */

import { afterEach, describe, expect, it } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { ExpectedRequest } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useContentApi } from '~/composables/apis/useContentApi'
import { LegalPage } from '~/constants/legalPage'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useContentApi()

/** Every endpoint function with the request it must send. */
const rows: ExpectedRequest[] = [
  { name: 'fetchAnnouncements reads the list', call: () => api.fetchAnnouncements(), method: HttpMethod.Get, url: '/api/portal/client/announcements' },
  { name: 'fetchRecentAnnouncements reads the dashboard summaries', call: () => api.fetchRecentAnnouncements(), method: HttpMethod.Get, url: '/api/portal/public/announcements' },
  { name: 'fetchAnnouncement reads one announcement by id', call: () => api.fetchAnnouncement('11'), method: HttpMethod.Get, url: '/api/portal/public/announcements/11' },
  { name: 'fetchLegalText reads the operator text of one legal page', call: () => api.fetchLegalText(LegalPage.AcceptableUse), method: HttpMethod.Get, url: '/api/portal/public/legal/acceptable_use' },
  { name: 'fetchKbCategories reads the list', call: () => api.fetchKbCategories(), method: HttpMethod.Get, url: '/api/portal/knowledgebase/categories' },
  { name: 'fetchKbArticles sends the filters as a query', call: () => api.fetchKbArticles({ category: 'Email', pageSize: 100 }), method: HttpMethod.Get, url: '/api/portal/knowledgebase/articles?category=Email&pageSize=100' },
  { name: 'fetchKbArticles sends a search term', call: () => api.fetchKbArticles({ search: 'vps', pageSize: 50 }), method: HttpMethod.Get, url: '/api/portal/knowledgebase/articles?search=vps&pageSize=50' },
  { name: 'fetchKbArticle reads one article by id', call: () => api.fetchKbArticle('12'), method: HttpMethod.Get, url: '/api/portal/knowledgebase/12' },
]

describe('useContentApi', () => {
  it.each(rows)('$name', async (row) => {
    stub = await startTransport()

    await row.call()

    expectLastRequest(stub, row)
  })

  it('returns the article page as the endpoint promises', async () => {
    stub = await startTransport(() => ({ body: { items: [{ id: 1, title: 'a' }] } }))

    const list = await api.fetchKbArticles({ pageSize: 10 })

    expect(list.items[0]?.title).toBe('a')
  })
})
