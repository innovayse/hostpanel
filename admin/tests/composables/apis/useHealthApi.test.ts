/**
 * Tests for the health API composable: the probe is read at the root, outside the API base, and never from a cache.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useHealthApi } from '@/composables/apis/useHealthApi'
import { json, problem, stubFetch, stubOffline } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useHealthApi', () => {
  it('getHealth reads /health outside the API base and asks for a fresh answer', async () => {
    const mock = stubFetch(json({ status: 'Healthy', timestamp: '2026-11-24T10:00:00Z', processStartedAt: '2026-11-24T09:00:00Z' }))

    expect(await useHealthApi().getHealth()).toEqual({
      status: 'Healthy',
      timestamp: '2026-11-24T10:00:00Z',
      processStartedAt: '2026-11-24T09:00:00Z',
    })
    const [url, init] = mock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/health')
    expect(init.cache).toBe('no-store')
  })

  it('rejects with the server status when it answers a refusal', async () => {
    stubFetch(problem('Starting.', 503))

    await expect(useHealthApi().getHealth()).rejects.toMatchObject({ status: 503 })
  })

  it('rejects while nothing answers', async () => {
    stubOffline()

    await expect(useHealthApi().getHealth()).rejects.toBeInstanceOf(TypeError)
  })
})
