/**
 * Unit tests for EmailCampaignStats: every counter is shown under its caption, and the failed
 * tile turns red only once something failed.
 */
import { describe, expect, it } from 'vitest'
import EmailCampaignStats from '@/components/campaigns/EmailCampaignStats.vue'
import { mountWithApp } from '../../helpers/mount'

/** Counters of a campaign with nothing failed. */
const clean = { total: 40, pending: 10, sent: 28, failed: 0, unsubscribed: 2 }

describe('EmailCampaignStats', () => {
  it('shows every counter under its caption', () => {
    const wrapper = mountWithApp(EmailCampaignStats, { props: { stats: clean } })

    expect(wrapper.get('[data-testid="email-campaign-stat-total"]').text()).toContain('40')
    expect(wrapper.get('[data-testid="email-campaign-stat-total"]').text()).toContain('Recipients')
    expect(wrapper.get('[data-testid="email-campaign-stat-pending"]').text()).toContain('10')
    expect(wrapper.get('[data-testid="email-campaign-stat-sent"]').text()).toContain('28')
    expect(wrapper.get('[data-testid="email-campaign-stat-failed"]').text()).toContain('Failed')
    expect(wrapper.get('[data-testid="email-campaign-stat-unsubscribed"]').text()).toContain('2')
  })

  it('draws the failed counter in the danger colour only when something failed', () => {
    const calm = mountWithApp(EmailCampaignStats, { props: { stats: clean } })
    const failing = mountWithApp(EmailCampaignStats, { props: { stats: { ...clean, failed: 3 } } })

    expect(calm.get('[data-testid="email-campaign-stat-failed"] p.font-display').classes().join(' ')).not.toContain('text-status-red')
    expect(failing.get('[data-testid="email-campaign-stat-failed"] p.font-display').classes().join(' ')).toContain('text-status-red')
  })
})
