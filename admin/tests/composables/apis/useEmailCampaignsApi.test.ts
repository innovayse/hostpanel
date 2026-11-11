/**
 * Tests for the email campaigns API composable: paths, verbs and bodies through the real
 * `useApi` transport with `fetch` stubbed.
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { useEmailCampaignsApi } from '@/composables/apis/useEmailCampaignsApi'
import { CampaignFilterType } from '@/types/emailcampaign'
import { callOf, json, stubFetch } from '../../helpers/fetch'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** A valid campaign body. */
const payload = {
  subject: 'Hello',
  body: '<p>Hi</p>',
  segments: [{ filterType: CampaignFilterType.Country, filterValue: 'AM' }],
}

describe('useEmailCampaignsApi', () => {
  it('listCampaigns reads the collection', async () => {
    const mock = stubFetch(json([{ id: 1 }]))

    expect(await useEmailCampaignsApi().listCampaigns()).toEqual([{ id: 1 }])
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-campaigns', method: 'GET' })
  })

  it('getCampaign reads one campaign', async () => {
    const mock = stubFetch(json({ id: 4 }))

    expect(await useEmailCampaignsApi().getCampaign(4)).toEqual({ id: 4 })
    expect(callOf(mock).url).toBe('/api/admin/email-campaigns/4')
  })

  it('createCampaign POSTs the payload and returns the id', async () => {
    const mock = stubFetch(json(12, 201))

    expect(await useEmailCampaignsApi().createCampaign(payload)).toBe(12)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-campaigns', method: 'POST', body: payload })
  })

  it('updateCampaign PUTs and deleteCampaign DELETEs', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), new Response(null, { status: 204 }))
    const api = useEmailCampaignsApi()

    await api.updateCampaign(4, payload)
    await api.deleteCampaign(4)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/email-campaigns/4', method: 'PUT', body: payload })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/email-campaigns/4', method: 'DELETE' })
  })

  it('scheduleCampaign sends the moment, or null to send now', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }), new Response(null, { status: 204 }))
    const api = useEmailCampaignsApi()

    await api.scheduleCampaign(4, '2026-12-01T10:00:00.000Z')
    await api.scheduleCampaign(4, null)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/email-campaigns/4/schedule', method: 'POST', body: { at: '2026-12-01T10:00:00.000Z' } })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/email-campaigns/4/schedule', method: 'POST', body: { at: null } })
  })

  it('cancelCampaign POSTs the cancel action', async () => {
    const mock = stubFetch(new Response(null, { status: 204 }))

    await useEmailCampaignsApi().cancelCampaign(4)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-campaigns/4/cancel', method: 'POST' })
  })

  it('previewCampaign reads the rendered mail', async () => {
    const mock = stubFetch(json({ subject: 'Hello', html: '<p>Hi</p>' }))

    expect(await useEmailCampaignsApi().previewCampaign(4)).toEqual({ subject: 'Hello', html: '<p>Hi</p>' })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-campaigns/4/preview', method: 'GET' })
  })

  it('countAudience POSTs the segments and returns the count', async () => {
    const mock = stubFetch(json({ count: 31 }))

    expect(await useEmailCampaignsApi().countAudience(payload.segments)).toEqual({ count: 31 })
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-campaigns/audience-count', method: 'POST', body: { segments: payload.segments } })
  })
})
