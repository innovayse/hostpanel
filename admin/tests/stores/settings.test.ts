/**
 * Tests for the settings store: the branding upload, setting updates and product mutations
 * report success as a value and keep a refusal in state instead of throwing.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ContentLanguage } from '@/locales/contentLanguage'
import { useSettingsStore } from '@/stores/settings'
import { ProductStatus, ProductType } from '@/types/productenums'
import { callOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'
import { stubRouteTable } from '../helpers/routes'
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

    expect(await useSettingsStore().uploadBrandingFile('logo', file)).toEqual({ url: '/img/logo.png', error: null })
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/admin/settings/branding/logo')
  })

  it('posts every slot to the endpoint of its own kind', async () => {
    const fetchMock = stubFetch(json({ url: '/a' }), json({ url: '/b' }))
    const store = useSettingsStore()

    await store.uploadBrandingFile('logodark', file)
    await store.uploadBrandingFile('logo', file)

    expect(fetchMock.mock.calls.map((call) => call[0])).toEqual([
      '/api/admin/settings/branding/logodark',
      '/api/admin/settings/branding/logo',
    ])
  })

  it('returns no url and the detail sentence of a problem+json refusal', async () => {
    stubFetch(problem('Unsupported image type.'))

    expect(await useSettingsStore().uploadBrandingFile('favicon', file)).toEqual({ url: null, error: 'Unsupported image type.' })
  })

  it('keeps the fallback when nothing answered', async () => {
    stubOffline()

    expect(await useSettingsStore().uploadBrandingFile('favicon', file)).toEqual({
      url: null,
      error: 'Could not reach the server. Check your connection and try again.',
    })
  })

  it('adds the declared size to the sentence when the image has too many pixels', async () => {
    stubFetch(invalid('That image has too many pixels.', { dimensions: ['18641x8668'] }))

    const outcome = await useSettingsStore().uploadBrandingFile('logo', file)

    expect(outcome.url).toBeNull()
    expect(outcome.error).toBe('That image has too many pixels. Your image is 18641 × 8668 pixels.')
  })

  it('gives each of two uploads running at once the answer to its own request', async () => {
    stubRouteTable({
      'POST /admin/settings/branding/logo': () => problem('That image is too large.', 413),
      'POST /admin/settings/branding/favicon': () => json({ url: '/ok.png' }),
    })
    const store = useSettingsStore()

    const [light, favicon] = await Promise.all([
      store.uploadBrandingFile('logo', file),
      store.uploadBrandingFile('favicon', file),
    ])

    expect(light).toEqual({ url: null, error: 'That image is too large.' })
    expect(favicon).toEqual({ url: '/ok.png', error: null })
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

describe('settingsStore.saveSetting', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('answers that the value was stored, with no sentence', async () => {
    stubFetch(json({}), json([{ id: 1, key: 'a', value: 'b' }]))

    expect(await useSettingsStore().saveSetting(1, 'b')).toEqual({ saved: true, error: null })
  })

  it('answers the API sentence of a refusal', async () => {
    stubFetch(problem('Allowed values: Cba, Ecb.'))

    expect(await useSettingsStore().saveSetting(1, 'Boe')).toEqual({ saved: false, error: 'Allowed values: Cba, Ecb.' })
  })

  it('gives each of two saves running at once the answer to its own request, and leaves the shared banner alone', async () => {
    stubRouteTable({
      'PUT /admin/settings/1': () => problem('Allowed values: https addresses.', 400),
      'PUT /admin/settings/2': () => new Response(null, { status: 204 }),
      'GET /admin/settings': () => json([]),
    })
    const store = useSettingsStore()

    const [refused, stored] = await Promise.all([store.saveSetting(1, 'x'), store.saveSetting(2, 'y')])

    expect(refused).toEqual({ saved: false, error: 'Allowed values: https addresses.' })
    expect(stored).toEqual({ saved: true, error: null })
    expect(store.actionError).toBeNull()
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

    expect(await useSettingsStore().saveEmailTemplateVersion('welcome', ContentLanguage.Hy, null, content)).toBe(true)
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/admin/email-templates')
    expect((fetchMock.mock.calls[0]![1] as RequestInit).method).toBe('POST')
  })

  it('rewrites a stored version by its id', async () => {
    const fetchMock = stubFetch(new Response(null, { status: 204 }), json([]))

    expect(await useSettingsStore().saveEmailTemplateVersion('welcome', ContentLanguage.Ru, 4, content)).toBe(true)
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/admin/email-templates/4')
  })

  it('keeps the API sentence of a refused save', async () => {
    stubFetch(problem('This template already has a version in that language.', 409))
    const store = useSettingsStore()

    expect(await store.saveEmailTemplateVersion('welcome', ContentLanguage.Ru, null, content)).toBe(false)
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

  it('createProduct returns the new id and reads no list again', async () => {
    const mock = stubFetch(json(12))
    const store = useSettingsStore()

    expect(await store.createProduct({} as never)).toBe(12)
    expect(store.actionError).toBeNull()
    expect(mock).toHaveBeenCalledTimes(1)
  })

  it('createProduct returns null and sets actionError on refusal', async () => {
    stubFetch(problem('Slug already used.'))
    const store = useSettingsStore()

    expect(await store.createProduct({} as never)).toBeNull()
    expect(store.actionError).toBe('Slug already used.')
  })

  it('keeps the API field errors of a refused save and clears them on the next try', async () => {
    stubFetch(invalid('Validation failed.', { Metadata: ['Not an object.'], GracePeriodDays: ['Out of range.'] }), json(12))
    const store = useSettingsStore()

    expect(await store.createProduct({} as never)).toBeNull()
    expect(store.fieldErrors).toEqual({ metadata: 'Not an object.', gracePeriodDays: 'Out of range.' })

    expect(await store.createProduct({} as never)).toBe(12)
    expect(store.fieldErrors).toEqual({})
  })

  it('drops the message of one field when the operator changes it and keeps the others', () => {
    const store = useSettingsStore()
    store.fieldErrors = { sku: 'SKU taken.', metadata: 'Too large.' }

    store.clearFieldError('sku')

    expect(store.fieldErrors).toEqual({ metadata: 'Too large.' })
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
    const mock = stubFetch(json(5))
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

  it('fetchProductsPage fills the page and the total, and sends the page, its size and the trimmed filters', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], pageNumber: 2, pageSize: 20, totalCount: 21, totalPages: 2 }))
    const store = useSettingsStore()

    expect(await store.fetchProductsPage(2, { search: ' plan ', groupId: 5, type: ProductType.Vps, status: ProductStatus.Active })).toBe(true)
    expect(callOf(mock).url).toBe('/api/admin/products?page=2&pageSize=20&search=plan&groupId=5&type=Vps&status=Active')
    expect(store.productPage).toEqual([{ id: 1 }])
    expect(store.productTotalCount).toBe(21)
    expect(store.loading).toBe(false)
  })

  it('fetchProductsPage reports the API sentence and keeps the page it had', async () => {
    const store = useSettingsStore()
    store.productPage.push({ id: 9 } as never)
    stubFetch(problem('Catalogue locked.', 423))

    expect(await store.fetchProductsPage(1)).toBe(false)
    expect(store.error).toBe('Catalogue locked.')
    expect(store.productPage).toHaveLength(1)
    expect(store.products).toEqual([])
  })

  it('createProduct returns the id; a refusal returns null with actionError', async () => {
    const store = useSettingsStore()

    stubFetch(json(4))
    expect(await store.createProduct({} as never)).toBe(4)

    stubFetch(problem('Name taken.', 409))
    expect(await store.createProduct({} as never)).toBeNull()
    expect(store.actionError).toBe('Name taken.')
  })
})

describe('settingsStore product groups', () => {
  installStoreHooks()

  /** A group payload restricted to one method. */
  const payload = { name: 'Shared', description: null, allowedPaymentMethods: ['bank_transfer'] }

  it('fetchPaymentMethods fills the choices of the group form', async () => {
    const store = useSettingsStore()
    stubFetch(json([{ module: 'bank_transfer', displayName: 'Bank transfer' }]))

    await store.fetchPaymentMethods()

    expect(store.paymentMethods).toEqual([{ module: 'bank_transfer', displayName: 'Bank transfer' }])
  })

  it('createProductGroup returns the new id', async () => {
    stubFetch(json(7))

    expect(await useSettingsStore().createProductGroup(payload)).toBe(7)
  })

  it('updateProductGroup reports success as true', async () => {
    stubFetch(new Response(null, { status: 204 }))

    expect(await useSettingsStore().updateProductGroup(7, payload)).toBe(true)
  })

  it('a refused group save returns the failure and keeps the API sentence', async () => {
    const store = useSettingsStore()
    stubFetch(problem('That group is gone.', 404))

    expect(await store.updateProductGroup(7, payload)).toBe(false)
    expect(await store.createProductGroup(payload)).toBeNull()
    expect(store.actionError).toBe('That group is gone.')
  })
})
