/**
 * Mount tests for ProviderStatusCard: the connection badge, the figures only while connected, the
 * last test time, the provider's error, and the test event.
 */
import { describe, expect, it } from 'vitest'
import ProviderStatusCard from '@/components/integrations/ProviderStatusCard.vue'
import { mountWithApp } from '../../helpers/mount'

/** Figures the page passes in. */
const metrics = [
  { label: 'Accounts', value: 42, large: true },
  { label: 'CWP Version', value: '0.9.8', mono: true },
]

/**
 * Mounts the card.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountCard = (props: Record<string, unknown> = {}) =>
  mountWithApp(ProviderStatusCard, { props: { info: null, metrics, testing: false, ...props } })

describe('ProviderStatusCard', () => {
  it('says the status is unknown before any info arrives', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="provider-status-connection"]').text()).toBe('Status Unknown')
    expect(wrapper.text()).not.toContain('42')
    expect(wrapper.get('[data-testid="provider-status-last-tested"]').text()).toBe('Never')
  })

  it('shows the figures and the test time while connected', () => {
    const wrapper = mountCard({
      info: { connected: true, lastTestedAt: new Date(Date.now() - 2 * 3600000).toISOString(), errorMessage: null },
    })

    expect(wrapper.get('[data-testid="provider-status-connection"]').text()).toBe('Connected')
    expect(wrapper.text()).toContain('42')
    expect(wrapper.text()).toContain('0.9.8')
    expect(wrapper.get('[data-testid="provider-status-last-tested"]').text()).toContain('hours ago')
  })

  it('shows the provider error and hides the figures when not connected', () => {
    const wrapper = mountCard({ info: { connected: false, lastTestedAt: null, errorMessage: 'Timed out.' } })

    expect(wrapper.get('[data-testid="provider-status-connection"]').text()).toBe('Not Connected')
    expect(wrapper.get('[data-testid="provider-status-error"]').text()).toContain('Timed out.')
    expect(wrapper.text()).not.toContain('42')
  })

  it('emits test from the button', async () => {
    const wrapper = mountCard()

    await wrapper.get('[data-testid="provider-status-test"]').trigger('click')

    expect(wrapper.emitted('test')).toHaveLength(1)
  })

  it('draws the large figure as a kit metric and no label in capitals', () => {
    const wrapper = mountCard({ info: { connected: true, lastTestedAt: null, errorMessage: null } })

    expect(wrapper.get('p.font-display').text()).toBe('42')
    expect(wrapper.find('.uppercase').exists()).toBe(false)
  })
})
