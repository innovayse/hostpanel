/**
 * Smoke tests for EmailCampaignsListView: the list with status badges, dates and delivery
 * counters, delete offered for drafts only and confirmed first, and the empty and error states
 * (with Retry), through the real store and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import EmailCampaignsListView from '@/views/EmailCampaignsListView.vue'
import { CampaignStatus } from '@/types/emailcampaign'
import { callOf, json, problem, stubFetch } from '../helpers/fetch'
import { mountView } from '../helpers/mount'

/** The delivery counters of a campaign nothing has been sent for. */
const untouched = { total: 0, pending: 0, sent: 0, failed: 0, unsubscribed: 0 }

/** A draft as the list endpoint returns it. */
const draft = {
  id: 3,
  subject: 'Spring offer',
  status: CampaignStatus.Draft,
  scheduledAt: null,
  sentAt: null,
  createdAt: '2026-10-01T10:30:00Z',
  stats: untouched,
}

/** A finished campaign as the list endpoint returns it. */
const sent = {
  id: 4,
  subject: 'Maintenance notice',
  status: CampaignStatus.Sent,
  scheduledAt: '2026-10-02T08:00:00Z',
  sentAt: '2026-10-02T08:05:00Z',
  createdAt: '2026-10-01T11:00:00Z',
  stats: { total: 40, pending: 0, sent: 36, failed: 1, unsubscribed: 3 },
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('EmailCampaignsListView', () => {
  it('lists the campaigns with status, subject and delivery counters', async () => {
    stubFetch(json([draft, sent]))

    const wrapper = await mountView(EmailCampaignsListView)

    expect(wrapper.get('[data-testid="email-campaigns-row-3"]').text()).toContain('Spring offer')
    expect(wrapper.get('[data-testid="email-campaigns-row-3"]').text()).toContain('Draft')
    const row = wrapper.get('[data-testid="email-campaigns-row-4"]').text()
    expect(row).toContain('Maintenance notice')
    expect(row).toContain('Sent')
    expect(row).toContain('36 sent, 1 failed, 3 unsubscribed')
  })

  it('offers delete for a draft only', async () => {
    stubFetch(json([draft, sent]))
    const wrapper = await mountView(EmailCampaignsListView)

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    expect(wrapper.find('[data-testid="email-campaigns-3-delete"]').exists()).toBe(true)

    expect(wrapper.find('[data-testid="email-campaigns-4-delete"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="email-campaigns-4-open"]').text()).toBe('View')
  })

  it('asks for confirmation before deleting, then deletes, reports it and reloads', async () => {
    const mock = stubFetch(json([draft]), new Response(null, { status: 204 }), json([]))
    const wrapper = await mountView(EmailCampaignsListView)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(callOf(mock, 1)).toMatchObject({ url: '/api/admin/email-campaigns/3', method: 'DELETE' })
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="email-campaigns-feedback"]').text()).toBe('Campaign deleted.')
    expect(wrapper.find('[data-testid="email-campaigns-row-3"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    const mock = stubFetch(json([draft]))
    const wrapper = await mountView(EmailCampaignsListView)

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock).toHaveBeenCalledTimes(1)
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('shows the API sentence when the delete is refused and keeps the list', async () => {
    stubFetch(json([draft]), problem('Only a draft can be deleted.', 409))
    const wrapper = await mountView(EmailCampaignsListView)

    await wrapper.get('[data-testid="email-campaigns-3-actions"]').trigger('click')
    await wrapper.get('[data-testid="email-campaigns-3-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="email-campaigns-feedback"]').text()).toBe('Only a draft can be deleted.')
    expect(wrapper.find('[data-testid="email-campaigns-row-3"]').exists()).toBe(true)
  })

  it('shows the empty state when there are no campaigns', async () => {
    stubFetch(json([]))

    const wrapper = await mountView(EmailCampaignsListView)

    expect(wrapper.get('[data-testid="email-campaigns-table"]').text()).toContain('No campaigns yet')
  })

  it('shows the API sentence when the list is refused, and reloads from the Retry', async () => {
    stubFetch(problem('Not allowed.', 403), json([draft]))
    const wrapper = await mountView(EmailCampaignsListView)
    expect(wrapper.get('[data-testid="email-campaigns-error"]').text()).toBe('Not allowed.')

    await wrapper.get('[data-testid="email-campaigns-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="email-campaigns-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="email-campaigns-row-3"]').exists()).toBe(true)
  })

  it('links the add button to the new campaign form', async () => {
    stubFetch(json([]))

    const wrapper = await mountView(EmailCampaignsListView)

    expect(wrapper.get('[data-testid="email-campaigns-add"]').attributes('href')).toBe('/support/email-campaigns/new')
  })
})
