/**
 * Tests for the gateway log API composable: the list endpoint through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useGatewayLogApi } from '@/composables/apis/useGatewayLogApi'
import { GatewayLogResult } from '@/types/gatewaylog'
import { json, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useGatewayLogApi', () => {
  it('list pages the endpoint and omits unset filters', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))

    await useGatewayLogApi().list({}, 1, 20)

    expect(callOf(mock).url).toBe('/api/gateway-logs?page=1&pageSize=20')
  })

  it('list sends the search, the period as bare days and the result', async () => {
    const mock = stubFetch(json({ items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0 }))

    await useGatewayLogApi().list(
      { search: 'bank', from: '2026-10-01', to: '2026-10-02', result: GatewayLogResult.Pending },
      2,
      10,
    )

    const query = new URLSearchParams(callOf(mock).url.split('?')[1])
    expect(query.get('from')).toBe('2026-10-01')
    expect(query.get('to')).toBe('2026-10-02')
    expect(query.get('search')).toBe('bank')
    expect(query.get('result')).toBe('Pending')
    expect(query.get('page')).toBe('2')
    expect(query.has('gateway')).toBe(false)
    expect(query.has('debugData')).toBe(false)
  })
})
