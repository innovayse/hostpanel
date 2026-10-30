/**
 * Tests for the settings store: the branding upload, setting updates and product mutations
 * report success as a value and keep a refusal in state instead of throwing.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { MessageLanguage } from '@/locales/messages'
import { useSettingsStore } from '@/stores/settings'
import { callOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { installStoreHooks, NO_ANSWER } from '../helpers/storeHooks'

/** A file standing in for the picked image. */
const file = new File(['x'], 'logo.png', { type: 'image/png' })

describe('settingsStore.uploadBrandingFile', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts to the branding endpoint of the kind and returns the url', async () => {
    const fetchMock = stubFetch(json({ url: '/img/logo.png' }))

    expect(await useSettingsStore().uploadBrandingFile('logo', file)).toBe('/img/logo.png')
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/admin/settings/branding/logo')
  })

  it('returns null and keeps the detail sentence of a problem+json refusal', async () => {
    stubFetch(problem('Unsupported image type.'))
    const store = useSettingsStore()

    expect(await store.uploadBrandingFile('favicon', file)).toBeNull()
    expect(store.uploadError).toBe('Unsupported image type.')
  })

  it('keeps the fallback when nothing answered', async () => {
    stubOffline()
    const store = useSettingsStore()

    expect(await store.uploadBrandingFile('favicon', file)).toBeNull()
    expect(store.uploadError).toBe('Could not reach the server. Check your connection and try again.')
  })
})

describe('settingsStore.updateSetting', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns true and refreshes the list', async () => {
    const fetchMock = stubFetch(json({}), json([{ id: 1, key: 'a', value: 'b' }]))
    const store = useSettingsStore()

    expect(await store.updateSetting(1, 'b')).toBe(true)
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(store.settings).toHaveLength(1)
  })

  it('returns false with the sentence naming what the key accepts', async () => {
    stubFetch(problem('Allowed values: light, dark, system.'))
    const store = useSettingsStore()

    expect(await store.updateSetting(1, 'x')).toBe(false)
    expect(store.actionError).toBe('Allowed values: light, dark, system.')
    expect(store.error).toBeNull()
  })
})

describe('settingsStore.clearSetting', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns true and refreshes the list', async () => {
    const fetchMock = stubFetch(json({}), json([{ id: 1, key: 'integration:smtp:password', value: null, isSecret: true, hasValue: false }]))
    const store = useSettingsStore()

    expect(await store.clearSetting(1)).toBe(true)
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(store.settings[0]?.hasValue).toBe(false)
  })

  it('returns false with the API sentence when refused', async () => {
    stubFetch(problem('Setting not found.', 404))
    const store = useSettingsStore()

    expect(await store.clearSetting(1)).toBe(false)
    expect(store.actionError).toBe('Setting not found.')
  })
})

describe('settingsStore.saveEmailTemplateVersion', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  /** The words saved in every case. */
  const content = { subject: 'S', body: 'B', description: null }

  it('adds a version the template does not have yet and refreshes', async () => {
    const fetchMock = stubFetch(json(7), json([]))

    expect(await useSettingsStore().saveEmailTemplateVersion('welcome', MessageLanguage.Hy, null, content)).toBe(true)
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/admin/email-templates')
    expect((fetchMock.mock.calls[0]![1] as RequestInit).method).toBe('POST')
  })

  it('rewrites a stored version by its id', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }), json([]))

    expect(await useSettingsStore().saveEmailTemplateVersion('welcome', MessageLanguage.Ru, 4, content)).toBe(true)
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/admin/email-templates/4')
  })

  it('keeps the API sentence of a refused save', async () => {
    stubFetch(problem('This template already has a version in that language.', 409))
    const store = useSettingsStore()

    expect(await store.saveEmailTemplateVersion('welcome', MessageLanguage.Ru, null, content)).toBe(false)
    expect(store.actionError).toBe('This template already has a version in that language.')
  })
})

describe('settingsStore products', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('createProduct returns the new id and refreshes', async () => {
    stubFetch(json(12), json([]))
    const store = useSettingsStore()

    expect(await store.createProduct({} as never)).toBe(12)
    expect(store.actionError).toBeNull()
  })

  it('createProduct returns null and sets actionError on refusal', async () => {
    stubFetch(problem('Slug already used.'))
    const store = useSettingsStore()

    expect(await store.createProduct({} as never)).toBeNull()
    expect(store.actionError).toBe('Slug already used.')
  })

  it('keeps the API field errors of a refused save and clears them on the next try', async () => {
    stubFetch(invalid('Validation failed.', { Metadata: ['Not an object.'], GracePeriodDays: ['Out of range.'] }), json(12), json([]))
    const store = useSettingsStore()

    expect(await store.createProduct({} as never)).toBeNull()
    expect(store.fieldErrors).toEqual({ metadata: 'Not an object.', gracePeriodDays: 'Out of range.' })

    expect(await store.createProduct({} as never)).toBe(12)
    expect(store.fieldErrors).toEqual({})
  })

  it('puts a taken SKU on the sku field instead of the form-level sentence', async () => {
    stubFetch(new Response(
      JSON.stringify({ type: 'PRODUCT_SKU_ALREADY_IN_USE', title: 'Conflict', status: 409, detail: 'Another product already uses this SKU.' }),
      { status: 409 },
    ))
    const store = useSettingsStore()

    expect(await store.updateProduct(1, {} as never)).toBe(false)
    expect(store.fieldErrors).toEqual({ sku: 'Another product already uses this SKU.' })
    expect(store.actionError).toBeNull()
  })

  it('sends the SKU, metadata and grace period in the create body', async () => {
    const mock = stubFetch(json(5), json([]))
    const store = useSettingsStore()

    await store.createProduct({ sku: 'A-1', metadata: '{}', gracePeriodDays: 3 } as never)

    expect(callOf(mock).body).toMatchObject({ sku: 'A-1', metadata: '{}', gracePeriodDays: 3 })
  })

  it('updateProduct returns false offline with the fallback sentence', async () => {
    stubOffline()
    const store = useSettingsStore()

    expect(await store.updateProduct(1, {} as never)).toBe(false)
    expect(store.actionError).toBe('Could not reach the server. Check your connection and try again.')
  })
})

describe('settingsStore loads', () => {
  installStoreHooks()

  it('fetchSettings, fetchEmailTemplates, fetchProducts and fetchProductGroups fill their lists', async () => {
    const store = useSettingsStore()

    stubFetch(json([{ id: 1 }]))
    await store.fetchSettings()
    await store.fetchEmailTemplates()
    await store.fetchProducts()
    await store.fetchProductGroups()
    expect(store.settings).toEqual([{ id: 1 }])
    expect(store.emailTemplates).toEqual([{ id: 1 }])
    expect(store.products).toEqual([{ id: 1 }])
    expect(store.productGroups).toEqual([{ id: 1 }])
    expect(store.loading).toBe(false)
  })

  it('a refused load keeps the API sentence and the previous list', async () => {
    const store = useSettingsStore()
    store.products.push({ id: 9 } as never)
    stubFetch(problem('Catalogue locked.', 423))

    await store.fetchProducts()
    expect(store.error).toBe('Catalogue locked.')
    expect(store.products).toHaveLength(1)
    expect(store.loading).toBe(false)
  })

  it('an unreachable server leaves the no-answer sentence', async () => {
    stubOffline()
    const store = useSettingsStore()

    await store.fetchProducts()
    expect(store.error).toBe(NO_ANSWER)
  })

  it('createProduct returns the id and refreshes; a refusal returns null with actionError', async () => {
    const store = useSettingsStore()

    stubFetch(json(4), json([{ id: 4 }]))
    expect(await store.createProduct({} as never)).toBe(4)
    expect(store.products).toEqual([{ id: 4 }])

    stubFetch(problem('Name taken.', 409))
    expect(await store.createProduct({} as never)).toBeNull()
    expect(store.actionError).toBe('Name taken.')
  })
})
