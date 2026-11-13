/**
 * Tests for the branding store: values read and trimmed, dark fallback, one load only,
 * silent on failure.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBrandingStore } from '@/stores/branding'
import { json, problem, stubFetch, stubOffline } from '../helpers/fetch'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('brandingStore', () => {
  it('load keeps the trimmed branding values', async () => {
    stubFetch(json([
      { key: 'portal.logo', value: ' /l.png ' },
      { key: 'portal.favicon', value: '/f.ico' },
    ]))
    const store = useBrandingStore()

    await store.load()
    expect(store.logoUrl).toBe('/l.png')
    expect(store.faviconUrl).toBe('/f.ico')
    expect(store.logoOnDarkUrl).toBe('/l.png')
    expect(store.logoOnLightUrl).toBe('/l.png')
  })

  it('load uses a lone dark logo on light surfaces too', async () => {
    stubFetch(json([{ key: 'portal.logo.dark', value: '/d.png' }]))
    const store = useBrandingStore()

    await store.load()
    expect(store.logoOnLightUrl).toBe('/d.png')
    expect(store.logoOnDarkUrl).toBe('/d.png')
  })

  it('hasLogo is true once either logo is set', async () => {
    stubFetch(json([{ key: 'portal.logo.dark', value: '/d.png' }]))
    const store = useBrandingStore()
    expect(store.hasLogo).toBe(false)

    await store.load()
    expect(store.hasLogo).toBe(true)
  })

  it('load prefers the dark logo on dark surfaces', async () => {
    stubFetch(json([
      { key: 'portal.logo', value: '/l.png' },
      { key: 'portal.logo.dark', value: '/d.png' },
    ]))
    const store = useBrandingStore()

    await store.load()
    expect(store.logoOnDarkUrl).toBe('/d.png')
  })

  it('load reaches the network once', async () => {
    const mock = stubFetch(json([]))
    const store = useBrandingStore()

    await store.load()
    await store.load()
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('load leaves the values empty on refusal or no answer', async () => {
    stubFetch(problem('Down.', 500))
    const refused = useBrandingStore()
    await refused.load()
    expect(refused.logoUrl).toBe('')
    expect(refused.loaded).toBe(true)

    setActivePinia(createPinia())
    stubOffline()
    const offline = useBrandingStore()
    await offline.load()
    expect(offline.faviconUrl).toBe('')
  })
})
