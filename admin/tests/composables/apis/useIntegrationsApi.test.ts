/**
 * Tests for the integrations API composable: paths, verbs and bodies through the real `useApi`
 * transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useIntegrationsApi } from '@/composables/apis/useIntegrationsApi'
import { json, problem, stubFetch, callOf } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useIntegrationsApi', () => {
  it('listIntegrations and getIntegration read their endpoints', async () => {
    const mock = stubFetch(json([]))
    const api = useIntegrationsApi()

    await api.listIntegrations()
    await api.getIntegration('bank-transfer')
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/integrations', method: 'GET' })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/integrations/bank-transfer', method: 'GET' })
  })

  it('saveIntegration PUTs the payload', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useIntegrationsApi().saveIntegration('bank-transfer', { enabled: true, config: {} } as never)
    expect(callOf(mock)).toMatchObject({
      url: '/api/admin/integrations/bank-transfer',
      method: 'PUT',
      body: { enabled: true, config: {} },
    })
  })

  it('testIntegration POSTs to test and returns the result', async () => {
    const mock = stubFetch(json({ success: true }))

    expect(await useIntegrationsApi().testIntegration('bank-transfer')).toEqual({ success: true })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/integrations/bank-transfer/test', method: 'POST' })
  })

  it('getCwpServerInfo reads the CWP server-info endpoint', async () => {
    const mock = stubFetch(json({}))

    await useIntegrationsApi().getCwpServerInfo()
    expect(callOf(mock).url).toBe('/api/admin/integrations/cwp/server-info')
  })

  it('rejects when the API refuses', async () => {
    stubFetch(problem('Required field missing.', 422))

    await expect(useIntegrationsApi().saveIntegration('x', {} as never)).rejects.toBeTruthy()
  })
})
