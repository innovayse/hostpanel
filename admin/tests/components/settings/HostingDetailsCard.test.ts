/**
 * Tests for HostingDetailsCard: the performance, installer, guarantee and support-response text
 * fields saved one by one, the SSL / DDoS / no hidden fees switches, and the unseeded warning.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import HostingDetailsCard from '@/components/settings/HostingDetailsCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** Every row the card edits, as the API seeds them: text rows empty or filled, switches empty. */
const rows: Setting[] = [
  { id: 1, key: 'portal.stats.response_time', value: '180 ms' },
  { id: 2, key: 'portal.stats.sites_hosted', value: '' },
  { id: 3, key: 'portal.stats.support_response', value: '' },
  { id: 4, key: 'portal.installer.app_count', value: '150+' },
  { id: 5, key: 'portal.guarantee.money_back_days', value: '30' },
  { id: 6, key: 'portal.support.response.chat', value: '' },
  { id: 7, key: 'portal.support.response.ticket', value: '' },
  { id: 8, key: 'portal.support.response.email', value: '' },
  { id: 9, key: 'portal.support.response.phone', value: '' },
  { id: 10, key: 'portal.features.ssl', value: 'true' },
  { id: 11, key: 'portal.features.ddos', value: '' },
  { id: 12, key: 'portal.guarantee.no_hidden_fees', value: '' },
]

/**
 * Reads the value of one text field.
 *
 * @param wrapper - The mounted card.
 * @param testid - The field's test id.
 * @returns The input's value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper'], testid: string): string =>
  (wrapper.get(`[data-testid="${testid}"]`).element as HTMLInputElement).value

describe('HostingDetailsCard', () => {
  it('shows the stored figures', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    expect(valueOf(wrapper, 'hosting-stats-response-time')).toBe('180 ms')
    expect(valueOf(wrapper, 'hosting-installer-app-count')).toBe('150+')
    expect(valueOf(wrapper, 'hosting-guarantee-money-back-days')).toBe('30')
    expect(valueOf(wrapper, 'hosting-support-response-chat')).toBe('')
  })

  it('saves an edited figure on its own', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    await wrapper.get('[data-testid="hosting-stats-sites-hosted"]').setValue('12,000+')
    await wrapper.get('[data-testid="hosting-save-stats-sites-hosted"]').trigger('click')

    expect(save.mock.calls).toEqual([[2, '12,000+']])
  })

  it('saves the support response time of a channel', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    await wrapper.get('[data-testid="hosting-support-response-phone"]').setValue('5 min')
    await wrapper.get('[data-testid="hosting-save-support-response-phone"]').trigger('click')

    expect(save.mock.calls).toEqual([[9, '5 min']])
  })

  it('limits the money back days to three characters', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    expect(wrapper.get('[data-testid="hosting-guarantee-money-back-days"]').attributes('maxlength')).toBe('3')
  })

  it('shows the switches by their stored value, an empty row being off', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    expect(wrapper.get('[data-testid="hosting-features-ssl"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.get('[data-testid="hosting-features-ddos"]').attributes('aria-checked')).toBe('false')
    expect(wrapper.get('[data-testid="hosting-guarantee-no-hidden-fees"]').attributes('aria-checked')).toBe('false')
  })

  it('turns a switch on and saves the literal true', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    await wrapper.get('[data-testid="hosting-features-ddos"]').trigger('click')
    await wrapper.get('[data-testid="hosting-features-ddos-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[11, 'true']])
  })

  it('turns a switch off and saves the literal false', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows })

    await wrapper.get('[data-testid="hosting-features-ssl"]').trigger('click')
    await wrapper.get('[data-testid="hosting-features-ssl-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[10, 'false']])
  })

  it('warns about a row the API never seeded and hides its control', async () => {
    const { wrapper } = await mountScreen(HostingDetailsCard, { save, settings: rows.filter((row) => row.key !== 'portal.stats.response_time') })

    const warnings = wrapper.findAll('[data-testid="setting-not-seeded"]')
    expect(warnings).toHaveLength(1)
    expect(warnings[0]?.text()).toContain('portal.stats.response_time')
    expect(wrapper.find('[data-testid="hosting-stats-response-time"]').exists()).toBe(false)
  })
})
