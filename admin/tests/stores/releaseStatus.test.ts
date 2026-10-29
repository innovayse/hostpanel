/**
 * Tests for the release status store: fetch fills `status`, a refusal puts the API's sentence
 * into `error`, and an unreachable API puts the shared no-answer sentence.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useReleaseStatusStore } from '@/stores/releaseStatus'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'

const dto = {
  installedVersion: '1.0.0',
  latestVersion: '2.0.0',
  releaseNotesUrl: 'https://github.com/innovayse/hostpanel/releases/tag/v2.0.0',
  isUpgradeAvailable: true,
}

describe('releaseStatusStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetch fills status and clears loading', async () => {
    stubFetch(json(dto))
    const store = useReleaseStatusStore()

    await store.fetch()

    expect(store.status).toEqual(dto)
    expect(store.loading).toBe(false)
    expect(store.error).toBeNull()
  })

  it('fetch sets loading to true during the request', async () => {
    let loadingDuringFetch = false
    const store = useReleaseStatusStore()
    vi.stubGlobal('fetch', vi.fn(async () => {
      loadingDuringFetch = store.loading
      return new Response(JSON.stringify(dto), { status: 200 })
    }))

    await store.fetch()

    expect(loadingDuringFetch).toBe(true)
    expect(store.loading).toBe(false)
  })

  it('fetch reports the API sentence on refusal', async () => {
    stubFetch(problem('No access.', 403))
    const store = useReleaseStatusStore()

    await store.fetch()

    expect(store.status).toBeNull()
    expect(store.error).toBe('No access.')
  })

  it('fetch reports the no-answer sentence when offline', async () => {
    stubOffline()
    const store = useReleaseStatusStore()

    await store.fetch()

    expect(store.status).toBeNull()
    expect(store.error).toBe(translate('errors.noAnswer'))
  })
})
