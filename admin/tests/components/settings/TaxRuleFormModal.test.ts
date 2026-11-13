/**
 * Tests for TaxRuleFormModal: create and edit modes, the emitted payload, the whole-country rule
 * and the refusal shown inside the dialog.
 */
import { describe, expect, it } from 'vitest'
import TaxRuleFormModal from '@/components/settings/TaxRuleFormModal.vue'
import UiCountrySelect from '@/components/ui/UiCountrySelect.vue'
import type { TaxRuleDto } from '@/types/taxrule'
import { mountScreen } from '../../helpers/mount'

/** A rule limited to a region. */
const bavaria: TaxRuleDto = { id: 3, name: 'Bavaria VAT', country: 'DE', region: 'Bavaria', ratePercent: 19, isEnabled: false }

/**
 * Reads the value of the native input behind a test id.
 *
 * @param wrapper - The mounted dialog.
 * @param testId - The `data-testid` of the field.
 * @returns The input's current value.
 */
const valueOf = (wrapper: Awaited<ReturnType<typeof mountScreen>>['wrapper'], testId: string): string =>
  (wrapper.get(`[data-testid="${testId}"]`).element as HTMLInputElement).value

describe('TaxRuleFormModal', () => {
  it('opens empty in create mode', async () => {
    const { wrapper } = await mountScreen(TaxRuleFormModal, { rule: null, saving: false, error: null })

    expect(wrapper.get('h2').text()).toBe('Add Tax Rule')
    expect(valueOf(wrapper, 'tax-rule-form-name')).toBe('')
    expect(valueOf(wrapper, 'tax-rule-form-region')).toBe('')
    expect(wrapper.get('[data-testid="tax-rule-form-save"]').text()).toBe('Add')
  })

  it('fills the fields from the edited rule', async () => {
    const { wrapper } = await mountScreen(TaxRuleFormModal, { rule: bavaria, saving: false, error: null })

    expect(wrapper.get('h2').text()).toBe('Edit Tax Rule')
    expect(valueOf(wrapper, 'tax-rule-form-name')).toBe('Bavaria VAT')
    expect(valueOf(wrapper, 'tax-rule-form-region')).toBe('Bavaria')
    expect(wrapper.getComponent(UiCountrySelect).props('modelValue')).toBe('DE')
    expect(wrapper.get('[data-testid="tax-rule-form-save"]').text()).toBe('Save')
  })

  it('emits the trimmed fields on submit, with a blank region as null', async () => {
    const { wrapper } = await mountScreen(TaxRuleFormModal, { rule: null, saving: false, error: null })

    await wrapper.get('[data-testid="tax-rule-form-name"]').setValue('  Germany VAT  ')
    wrapper.getComponent(UiCountrySelect).vm.$emit('update:modelValue', 'DE')
    await wrapper.get('[data-testid="tax-rule-form-region"]').setValue('   ')
    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      { name: 'Germany VAT', country: 'DE', region: null, ratePercent: 0, isEnabled: true },
    ]])
  })

  it('emits the region of a regional rule', async () => {
    const { wrapper } = await mountScreen(TaxRuleFormModal, { rule: bavaria, saving: false, error: null })

    await wrapper.get('[data-testid="tax-rule-form"]').trigger('submit')

    expect(wrapper.emitted('save')).toEqual([[
      { name: 'Bavaria VAT', country: 'DE', region: 'Bavaria', ratePercent: 19, isEnabled: false },
    ]])
  })

  it('shows the refusal inside the dialog', async () => {
    const { wrapper } = await mountScreen(TaxRuleFormModal, { rule: null, saving: false, error: 'A rule for this country exists.' })

    expect(wrapper.get('[data-testid="tax-rule-form-error"]').text()).toBe('A rule for this country exists.')
  })

  it('closes from Cancel and disables Save while saving', async () => {
    const { wrapper } = await mountScreen(TaxRuleFormModal, { rule: bavaria, saving: true, error: null })

    expect(wrapper.get('[data-testid="tax-rule-form-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="tax-rule-form-save"]').text()).toBe('Save')
    await wrapper.get('[data-testid="tax-rule-form-cancel"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
