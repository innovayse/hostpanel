/**
 * Tests for the email campaigns store: loads fill state, a refusal puts the API's sentence into
 * `error`, a refused save exposes the field messages, scheduling sends an ISO moment (or null to
 * send now), and only the newest audience count writes its answer. The requests go through the
 * real `useApi` transport.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { translate } from '@/lib/translate'
import { useEmailCampaignsStore } from '@/stores/emailCampaigns'
import { CampaignFilterType } from '@/types/emailcampaign'
import { callOf, invalid, json, problem, stubFetch, stubOffline } from '../helpers/fetch'

/** A valid campaign body. */
const payload = {
  subject: 'Hello',
  body: '<p>Hi</p>',
  segments: [{ filterType: CampaignFilterType.AllActive, filterValue: null }],
}

describe('emailCampaignsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetchAll fills the list', async () => {
    const mock = stubFetch(json([{ id: 1 }]))
    const store = useEmailCampaignsStore()

    expect(await store.fetchAll()).toBe(true)
    expect(callOf(mock).url).toBe('/api/admin/email-campaigns')
    expect(store.campaigns).toEqual([{ id: 1 }])
    expect(store.loading).toBe(false)
  })

  it('fetchAll reports the API sentence, and the no-answer sentence offline', async () => {
    const store = useEmailCampaignsStore()
    stubFetch(problem('Not allowed.', 403))
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe('Not allowed.')

    stubOffline()
    expect(await store.fetchAll()).toBe(false)
    expect(store.error).toBe(translate('errors.noAnswer'))
  })

  it('fetchById stores the campaign and clears it on refusal; clearCurrent forgets it', async () => {
    const store = useEmailCampaignsStore()
    stubFetch(json({ id: 3 }))
    expect(await store.fetchById(3)).toBe(true)
    expect(store.current).toEqual({ id: 3 })

    stubFetch(problem('No such campaign.', 404))
    expect(await store.fetchById(4)).toBe(false)
    expect(store.current).toBeNull()
    expect(store.error).toBe('No such campaign.')

    stubFetch(json({ id: 3 }))
    await store.fetchById(3)
    store.clearCurrent()
    expect(store.current).toBeNull()
  })

  it('create returns the id, null with field messages on refusal', async () => {
    const store = useEmailCampaignsStore()
    stubFetch(json(9, 201))
    expect(await store.create(payload)).toBe(9)

    stubFetch(invalid('Check the form.', { Subject: ['Subject is required.'] }))
    expect(await store.create({ ...payload, subject: '' })).toBeNull()
    expect(store.fieldErrors).toEqual({ subject: 'Subject is required.' })
    expect(store.loading).toBe(false)
  })

  it('update and remove report the outcome', async () => {
    const store = useEmailCampaignsStore()
    const mock = stubFetch(new Response(null, { status: 204 }), new Response(null, { status: 204 }))

    expect(await store.update(3, payload)).toBe(true)
    expect(await store.remove(3)).toBe(true)
    expect(callOf(mock, 0)).toMatchObject({ url: '/api/admin/email-campaigns/3', method: 'PUT', body: payload })
    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/email-campaigns/3', method: 'DELETE' })

    stubFetch(problem('Only a draft can be deleted.', 409))
    expect(await store.remove(3)).toBe(false)
    expect(store.error).toBe('Only a draft can be deleted.')
  })

  it('schedule sends the moment as ISO text, or null to send now', async () => {
    const store = useEmailCampaignsStore()
    const mock = stubFetch(new Response(null, { status: 204 }), new Response(null, { status: 204 }))

    expect(await store.schedule(3, new Date('2026-12-01T10:00:00.000Z'))).toBe(true)
    expect(await store.schedule(3, null)).toBe(true)
    expect(callOf(mock, 0).body).toEqual({ at: '2026-12-01T10:00:00.000Z' })
    expect(callOf(mock, 1).body).toEqual({ at: null })

    stubFetch(problem('The time is in the past.', 400))
    expect(await store.schedule(3, new Date('2020-01-01T00:00:00.000Z'))).toBe(false)
    expect(store.error).toBe('The time is in the past.')
  })

  it('cancel reports the outcome', async () => {
    const store = useEmailCampaignsStore()
    const mock = stubFetch(new Response(null, { status: 204 }))
    expect(await store.cancel(3)).toBe(true)
    expect(callOf(mock)).toMatchObject({ url: '/api/admin/email-campaigns/3/cancel', method: 'POST' })

    stubFetch(problem('Already sending.', 409))
    expect(await store.cancel(3)).toBe(false)
    expect(store.error).toBe('Already sending.')
  })

  it('fetchPreview keeps the rendered mail until it is cleared', async () => {
    const store = useEmailCampaignsStore()
    stubFetch(json({ subject: 'Hello', html: '<p>Hi</p>' }))
    expect(await store.fetchPreview(3)).toBe(true)
    expect(store.preview).toEqual({ subject: 'Hello', html: '<p>Hi</p>' })

    store.clearPreview()
    expect(store.preview).toBeNull()

    stubFetch(problem('Not found.', 404))
    expect(await store.fetchPreview(3)).toBe(false)
    expect(store.preview).toBeNull()
  })

  it('fetchAudienceCount stores the count and reports its own refusal apart from the form error', async () => {
    const store = useEmailCampaignsStore()
    const mock = stubFetch(json({ count: 31 }))
    expect(await store.fetchAudienceCount(payload.segments)).toBe(true)
    expect(callOf(mock)).toMatchObject({ method: 'POST', body: { segments: payload.segments } })
    expect(store.audienceCount).toBe(31)
    expect(store.counting).toBe(false)

    stubFetch(problem('Unknown product.', 400))
    expect(await store.fetchAudienceCount(payload.segments)).toBe(false)
    expect(store.audienceCount).toBeNull()
    expect(store.audienceError).toBe('Unknown product.')
    expect(store.error).toBeNull()
  })

  it('lets only the newest audience count write its answer', async () => {
    const store = useEmailCampaignsStore()
    let release: (response: Response) => void = () => undefined
    const slow = new Promise<Response>((resolve) => {
      release = resolve
    })
    vi.stubGlobal('fetch', vi.fn()
      .mockReturnValueOnce(slow)
      .mockResolvedValueOnce(json({ count: 2 })))

    const older = store.fetchAudienceCount(payload.segments)
    const newer = store.fetchAudienceCount(payload.segments)
    await newer
    release(json({ count: 99 }))

    expect(await older).toBe(false)
    expect(store.audienceCount).toBe(2)
  })

  it('clearAudience forgets the count and drops an answer still on its way', async () => {
    const store = useEmailCampaignsStore()
    stubFetch(json({ count: 5 }))
    await store.fetchAudienceCount(payload.segments)

    store.clearAudience()
    expect(store.audienceCount).toBeNull()
    expect(store.audienceError).toBeNull()
    expect(store.counting).toBe(false)
  })
})
