/**
 * Unit tests for EmailCampaignStats: every counter is shown under its caption, the failed tile
 * turns red only once something failed, and the open and click tiles show the totals, the unique
 * readers behind them and the rates as percentages.
 */
import { describe, expect, it } from 'vitest'
import EmailCampaignStats from '@/components/campaigns/EmailCampaignStats.vue'
import type { EmailCampaignStats as EmailCampaignStatsData } from '@/types/emailcampaign'
import { mountWithApp } from '../../helpers/mount'

/** Counters of a campaign with nothing failed: 28 mails delivered, 14 of them opened and 7 clicked. */
const clean: EmailCampaignStatsData = {
  total: 40,
  pending: 10,
  sent: 28,
  failed: 0,
  unsubscribed: 2,
  opened: 31,
  uniqueOpened: 14,
  clicked: 12,
  uniqueClicked: 7,
  openRate: 0.5,
  clickRate: 0.25
}

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

  it('shows the opens with the readers behind them, and the open rate as a percentage', () => {
    const wrapper = mountWithApp(EmailCampaignStats, { props: { stats: clean } })

    const opened = wrapper.get('[data-testid="email-campaign-stat-opened"]').text()
    expect(opened).toContain('Opens')
    expect(opened).toContain('31')
    expect(opened).toContain('14 unique')
    const rate = wrapper.get('[data-testid="email-campaign-stat-open-rate"]').text()
    expect(rate).toContain('Open rate')
    expect(rate).toContain('50%')
  })

  it('shows the clicks with the readers behind them, and the click rate as a percentage', () => {
    const wrapper = mountWithApp(EmailCampaignStats, { props: { stats: clean } })

    const clicked = wrapper.get('[data-testid="email-campaign-stat-clicked"]').text()
    expect(clicked).toContain('Clicks')
    expect(clicked).toContain('12')
    expect(clicked).toContain('7 unique')
    const rate = wrapper.get('[data-testid="email-campaign-stat-click-rate"]').text()
    expect(rate).toContain('Click rate')
    expect(rate).toContain('25%')
  })

  it('keeps one decimal in a rate that is not a whole percent', () => {
    const wrapper = mountWithApp(EmailCampaignStats, { props: { stats: { ...clean, openRate: 0.4167, clickRate: 0.0333 } } })

    expect(wrapper.get('[data-testid="email-campaign-stat-open-rate"]').text()).toContain('41.7%')
    expect(wrapper.get('[data-testid="email-campaign-stat-click-rate"]').text()).toContain('3.3%')
  })

  it('shows zeros for a campaign nobody has responded to', () => {
    const quiet = { ...clean, opened: 0, uniqueOpened: 0, clicked: 0, uniqueClicked: 0, openRate: 0, clickRate: 0 }
    const wrapper = mountWithApp(EmailCampaignStats, { props: { stats: quiet } })

    expect(wrapper.get('[data-testid="email-campaign-stat-open-rate"]').text()).toContain('0%')
    expect(wrapper.get('[data-testid="email-campaign-stat-clicked"]').text()).toContain('0 unique')
  })

  it('draws the failed counter in the danger colour only when something failed', () => {
    const calm = mountWithApp(EmailCampaignStats, { props: { stats: clean } })
    const failing = mountWithApp(EmailCampaignStats, { props: { stats: { ...clean, failed: 3 } } })

    expect(calm.get('[data-testid="email-campaign-stat-failed"] p.font-display').classes().join(' ')).not.toContain('text-status-red')
    expect(failing.get('[data-testid="email-campaign-stat-failed"] p.font-display').classes().join(' ')).toContain('text-status-red')
  })
})
