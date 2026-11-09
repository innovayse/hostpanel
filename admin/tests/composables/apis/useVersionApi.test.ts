/**
 * Tests for the installed version API composable: path, verb and typed result, through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useVersionApi } from '@/composables/apis/useVersionApi'
import { json, problem, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useVersionApi', () => {
  it('getInstalledVersion reads /admin/version with GET', async () => {
    const mock = stubFetch(json({ installedVersion: '1.4.0' }))

    expect(await useVersionApi().getInstalledVersion()).toEqual({ installedVersion: '1.4.0' })
    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/version')
    const init = mock.mock.calls[0]?.[1] as RequestInit | undefined
    expect(init?.method ?? 'GET').toBe('GET')
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('No access.', 403))

    await expect(useVersionApi().getInstalledVersion()).rejects.toBeTruthy()
  })
})
