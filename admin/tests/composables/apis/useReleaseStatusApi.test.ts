/**
 * Tests for the release status API composable: path, verb and typed result, through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useReleaseStatusApi } from '@/composables/apis/useReleaseStatusApi'
import { json, problem, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useReleaseStatusApi', () => {
  it('getReleaseStatus reads /admin/release-status', async () => {
    const dto = {
      installedVersion: '1.0.0',
      latestVersion: '2.0.0',
      releaseNotesUrl: 'https://github.com/innovayse/hostpanel/releases/tag/v2.0.0',
      isUpgradeAvailable: true,
    }
    const mock = stubFetch(json(dto))

    expect(await useReleaseStatusApi().getReleaseStatus()).toEqual(dto)
    expect(String(mock.mock.calls[0]?.[0])).toContain('/admin/release-status')
  })

  it('getReleaseStatus uses GET', async () => {
    stubFetch(json({ installedVersion: '1.0.0', latestVersion: null, releaseNotesUrl: null, isUpgradeAvailable: false }))

    await useReleaseStatusApi().getReleaseStatus()
    // fetch is called without an explicit method, which defaults to GET
    const [, init] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [unknown, RequestInit | undefined]
    expect(init?.method ?? 'GET').toBe('GET')
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('No access.', 403))

    await expect(useReleaseStatusApi().getReleaseStatus()).rejects.toBeTruthy()
  })
})
