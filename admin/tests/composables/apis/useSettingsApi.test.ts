/**
 * Tests for the settings API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed, and a refusal rejecting with the problem body.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useSettingsApi } from '@/composables/apis/useSettingsApi'
import { ContentLanguage } from '@/locales/contentLanguage'
import { ProductStatus, ProductType } from '@/types/productenums'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useSettingsApi', () => {
  it('listSettings reads /admin/settings', async () => {
    const mock = stubFetch(json([{ id: 1 }]))

    expect(await useSettingsApi().listSettings()).toEqual([{ id: 1 }])
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/settings', method: 'GET' })
  })

  it('updateSetting PUTs the value by id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useSettingsApi().updateSetting(4, 'on')
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/settings/4', method: 'PUT', body: { value: 'on' } })
  })

  it('listEmailTemplates reads its endpoint', async () => {
    const mock = stubFetch(json([]))

    await useSettingsApi().listEmailTemplates()
    expect(callOf(mock, 0).url).toBe('/api/admin/email-templates')
  })

  it('createEmailTemplate POSTs the new language version and returns its id', async () => {
    const mock = stubFetch(json(5))
    const payload = { slug: 'welcome', language: ContentLanguage.Ru, subject: 'S', body: 'B', description: null }

    expect(await useSettingsApi().createEmailTemplate(payload)).toBe(5)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-templates', method: 'POST', body: payload })
  })

  it('updateEmailTemplate PUTs the words by id', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useSettingsApi().updateEmailTemplate(2, { subject: 'S', body: 'B', description: 'D' })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-templates/2', method: 'PUT', body: { subject: 'S', body: 'B', description: 'D' } })
  })

  it('listProducts and listProductGroups ask for inactive rows too', async () => {
    const mock = stubFetch(json([]))
    const api = useSettingsApi()

    await api.listProducts()
    await api.listProductGroups()
    expect(callOf(mock, 0).url).toBe('/api/products?activeOnly=false&includeUnsellable=true')
    expect(callOf(mock, 1).url).toBe('/api/products/groups?activeOnly=false')
  })

  it('listProductsPage reads one page of /admin/products with the page, its size and the filters', async () => {
    const mock = stubFetch(json({ items: [{ id: 1 }], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }))
    const api = useSettingsApi()

    expect((await api.listProductsPage({ page: 1, pageSize: 20, search: 'plan', groupId: 5, type: ProductType.Vps, status: ProductStatus.Inactive })).items).toEqual([{ id: 1 }])
    await api.listProductsPage({ page: 3, pageSize: 20 })
    expect(callOf(mock, 0).url).toBe('/api/admin/products?page=1&pageSize=20&search=plan&groupId=5&type=Vps&status=Inactive')
    expect(callOf(mock, 1).url).toBe('/api/admin/products?page=3&pageSize=20')
  })

  it('createProduct POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(9))

    expect(await useSettingsApi().createProduct({ name: 'Plan' } as never)).toBe(9)
    expect(callOf(mock)).toMatchObject({ url: '/api/products', method: 'POST', body: { name: 'Plan' } })
  })

  it('updateProduct PUTs the payload with the id in the body', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useSettingsApi().updateProduct(3, { name: 'Plan' } as never)
    expect(callOf(mock)).toMatchObject({ url: '/api/products/3', method: 'PUT', body: { id: 3, name: 'Plan' } })
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('No access.', 403))

    await expect(useSettingsApi().listSettings()).rejects.toBeTruthy()
  })
})
