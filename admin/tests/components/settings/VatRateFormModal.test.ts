/**
 * Tests for VatRateFormModal: add and edit modes, the emitted payload, the guard on an empty member state or day, and
 * the refusal shown inside the dialog.
 */
import { describe, expect, it } from 'vitest'
import VatRateFormModal from '@/components/settings/VatRateFormModal.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { VatRateKind } from '@/types/vatrate'
import type { VatRateDto } from '@/types/vatrate'
import { mountScreen } from '../../helpers/mount'

/** A stored reduced rate. */
const reduced: VatRateDto = { id: 3, country: 'DE', kind: VatRateKind.Reduced, ratePercent: 7, validFrom: '2024-01-01' }

describe('VatRateFormModal', () => {
  it('opens empty in add mode, with the Add label', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: null, saving: false, error: null })

    expect(wrapper.get('h2').text()).toBe('Create VAT rate')
    expect(wrapper.getComponent(UiCountrySelect).props('modelValue')).toBe('')
    expect(wrapper.getComponent(UiDatePicker).props('modelValue')).toBeNull()
    expect(wrapper.get('[data-testid="vat-rate-form-save"]').text()).toBe('Add')
  })

  it('fills the fields from the edited row', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: reduced, saving: false, error: null })

    expect(wrapper.get('h2').text()).toBe('Edit VAT Rate')
    expect(wrapper.getComponent(UiCountrySelect).props('modelValue')).toBe('DE')
    expect(wrapper.getComponent(UiDatePicker).props('modelValue')).toBe('2024-01-01')
    expect(wrapper.get('[data-testid="vat-rate-form-save"]').text()).toBe('Save')
  })

  it('limits the member state picker to the EU member states', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: null, saving: false, error: null })

    const only = wrapper.getComponent(UiCountrySelect).props('only')

    expect(only).toHaveLength(27)
    expect(only).toContain('DE')
    expect(only).not.toContain('US')
  })

  it('emits the fields on submit, a standard rate by default', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: null, saving: false, error: null })

    wrapper.getComponent(UiCountrySelect).vm.$emit('update:modelValue', 'FR')
    wrapper.getComponent(UiDatePicker).vm.$emit('update:modelValue', '2025-07-01')
    await wrapper.get('[data-testid="vat-rate-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      { country: 'FR', kind: VatRateKind.Standard, ratePercent: 0, validFrom: '2025-07-01' },
    ]])
  })

  it('emits the stored fields of an edited row unchanged', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: reduced, saving: false, error: null })

    await wrapper.get('[data-testid="vat-rate-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      { country: 'DE', kind: VatRateKind.Reduced, ratePercent: 7, validFrom: '2024-01-01' },
    ]])
  })

  it('sends nothing and keeps Save disabled while the member state or the day is empty', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: null, saving: false, error: null })

    expect(wrapper.get('[data-testid="vat-rate-form-save"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="vat-rate-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toBeUndefined()
  })

  it('shows the refusal inside the dialog', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: null, saving: false, error: 'A VAT rate for this day exists.' })

    expect(wrapper.get('[data-testid="vat-rate-form-error"]').text()).toBe('A VAT rate for this day exists.')
  })

  it('closes from Cancel', async () => {
    const { wrapper } = await mountScreen(VatRateFormModal, { rate: reduced, saving: false, error: null })

    await wrapper.get('[data-testid="vat-rate-form-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
