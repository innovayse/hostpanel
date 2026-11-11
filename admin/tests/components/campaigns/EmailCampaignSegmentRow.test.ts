/**
 * Unit tests for EmailCampaignSegmentRow: each kind of rule shows the control for its value,
 * changing the kind clears the value, and edits and removal are reported to the form.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import EmailCampaignSegmentRow from '@/components/campaigns/EmailCampaignSegmentRow.vue'
import { CampaignFilterType } from '@/types/emailcampaign'
import type { EmailCampaignSegmentDraft } from '@/types/emailcampaign'
import { mountWithApp } from '../../helpers/mount'
import { pickOption, selectText } from '../../helpers/select'

/** The products the product rule offers in these tests. */
const products = [
  { value: '3', label: 'Basic hosting' },
  { value: '4', label: 'Business hosting' },
]

/**
 * Mounts the row for a rule.
 *
 * @param segment - The parts of the rule that differ from a blank all-active rule.
 * @returns The wrapper.
 */
const mountRow = (segment: Partial<EmailCampaignSegmentDraft> = {}): VueWrapper =>
  mountWithApp(EmailCampaignSegmentRow, {
    props: { modelValue: { key: 1, filterType: CampaignFilterType.AllActive, filterValue: '', ...segment }, products },
  })

/**
 * The rule the row last asked the form to store.
 *
 * @param wrapper - The mounted row.
 * @returns The last `update:modelValue` payload.
 */
const lastUpdate = (wrapper: VueWrapper): EmailCampaignSegmentDraft => {
  const events = wrapper.emitted('update:modelValue') ?? []
  return events[events.length - 1]?.[0] as EmailCampaignSegmentDraft
}

describe('EmailCampaignSegmentRow', () => {
  it('shows no value control for all active clients', () => {
    const wrapper = mountRow()

    expect(wrapper.find('[data-testid="email-campaign-segment-1-value"]').exists()).toBe(false)
    expect(selectText(wrapper, 'email-campaign-segment-1-type')).toBe('All active clients')
  })

  it('shows a product select that stores the product id', async () => {
    const wrapper = mountRow({ filterType: CampaignFilterType.Product })

    expect(selectText(wrapper, 'email-campaign-segment-1-value')).toBe('Choose a product')
    await pickOption(wrapper, 'email-campaign-segment-1-value', 'Business hosting')

    expect(lastUpdate(wrapper)).toEqual({ key: 1, filterType: CampaignFilterType.Product, filterValue: '4' })
  })

  it('shows the chosen product by name', () => {
    const wrapper = mountRow({ filterType: CampaignFilterType.Product, filterValue: '3' })

    expect(selectText(wrapper, 'email-campaign-segment-1-value')).toBe('Basic hosting')
  })

  it('shows a country select that stores the ISO code', async () => {
    const wrapper = mountRow({ filterType: CampaignFilterType.Country })

    expect(selectText(wrapper, 'email-campaign-segment-1-value')).toBe('Choose a country')
    await pickOption(wrapper, 'email-campaign-segment-1-value', 'Armenia')

    expect(lastUpdate(wrapper)).toEqual({ key: 1, filterType: CampaignFilterType.Country, filterValue: 'AM' })
  })

  it('shows a days field for the expiry rule and stores the number as text, or nothing when cleared', async () => {
    const wrapper = mountRow({ filterType: CampaignFilterType.ServiceExpiringInDays, filterValue: '30' })
    const field = wrapper.get('[data-testid="email-campaign-segment-1-value"] input')
    expect((field.element as HTMLInputElement).value).toBe('30')

    await field.setValue('45')
    expect(lastUpdate(wrapper).filterValue).toBe('45')

    await field.setValue('')
    expect(lastUpdate(wrapper).filterValue).toBe('')
  })

  it('clears the value when the kind of rule changes', async () => {
    const wrapper = mountRow({ filterType: CampaignFilterType.Country, filterValue: 'AM' })

    await pickOption(wrapper, 'email-campaign-segment-1-type', 'Clients with a service of a product')
    await flushPromises()

    expect(lastUpdate(wrapper)).toEqual({ key: 1, filterType: CampaignFilterType.Product, filterValue: '' })
  })

  it('asks the form to remove the row', async () => {
    const wrapper = mountRow()

    await wrapper.get('[data-testid="email-campaign-segment-1-remove"]').trigger('click')

    expect(wrapper.emitted('remove')).toHaveLength(1)
  })
})
