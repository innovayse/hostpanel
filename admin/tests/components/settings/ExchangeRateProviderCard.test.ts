/**
 * Tests for ExchangeRateProviderCard: the stored bank is shown by its label, the national bank is the
 * default for an empty row or a value that is no bank, a pick saves the literal enum string, a refusal
 * shows the API's sentence, and an unseeded row is replaced by a notice.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import ExchangeRateProviderCard from '@/components/settings/ExchangeRateProviderCard.vue'
import { ExchangeRateProvider } from '@/types/currency'
import { BillingSettingKey } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'
import { pickOption } from '../../helpers/select'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockReset()
  save.mockResolvedValue({ saved: true, error: null })
})

/**
 * Builds the props of the card for one stored value.
 *
 * @param value - The stored value of the row, or null for a row the server never seeded.
 * @returns The props.
 */
const propsFor = (value: string | null) => ({
  settings: (value === null ? [] : [{ id: 21, key: BillingSettingKey.RateProvider, value }]) as Setting[],
  save,
})

describe('ExchangeRateProviderCard', () => {
  it('shows the stored bank in the select', async () => {
    const { wrapper } = await mountScreen(ExchangeRateProviderCard, propsFor(ExchangeRateProvider.Ecb))

    expect(wrapper.get('[data-testid="exchange-rate-provider-select"]').text()).toContain('European Central Bank')
  })

  it('reads an empty row, or a value that is no bank, as the national bank', async () => {
    const empty = await mountScreen(ExchangeRateProviderCard, propsFor(''))
    expect(empty.wrapper.get('[data-testid="exchange-rate-provider-select"]').text()).toContain('Central Bank of Armenia')

    const unknown = await mountScreen(ExchangeRateProviderCard, propsFor('Boe'))
    expect(unknown.wrapper.get('[data-testid="exchange-rate-provider-select"]').text()).toContain('Central Bank of Armenia')
  })

  it('saves the picked bank as the literal string', async () => {
    const { wrapper } = await mountScreen(ExchangeRateProviderCard, propsFor(ExchangeRateProvider.Cba))

    await pickOption(wrapper, 'exchange-rate-provider-select', 'European Central Bank')
    await wrapper.get('[data-testid="exchange-rate-provider-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[21, ExchangeRateProvider.Ecb]])
  })

  it('does not save before anything changed', async () => {
    const { wrapper } = await mountScreen(ExchangeRateProviderCard, propsFor(ExchangeRateProvider.Cba))

    await wrapper.get('[data-testid="exchange-rate-provider-save"]').trigger('click')

    expect(save).not.toHaveBeenCalled()
  })

  it('shows the API sentence when the save is refused', async () => {
    save.mockResolvedValue({ saved: false, error: 'That value is not accepted for this setting.' })
    const { wrapper } = await mountScreen(ExchangeRateProviderCard, propsFor(ExchangeRateProvider.Cba))

    await pickOption(wrapper, 'exchange-rate-provider-select', 'European Central Bank')
    await wrapper.get('[data-testid="exchange-rate-provider-save"]').trigger('click')
    await wrapper.vm.$nextTick()

    expect(wrapper.text()).toContain('That value is not accepted for this setting.')
  })

  it('names the key and hides the select when the row was never seeded', async () => {
    const { wrapper } = await mountScreen(ExchangeRateProviderCard, propsFor(null))

    expect(wrapper.get('[data-testid="exchange-rate-provider-not-seeded"]').text()).toContain('billing.exchange_rates.provider')
    expect(wrapper.find('[data-testid="exchange-rate-provider-select"]').exists()).toBe(false)
  })
})
