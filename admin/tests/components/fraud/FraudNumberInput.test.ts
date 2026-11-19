/**
 * Tests for FraudNumberInput: the range, placeholder and test id come from the table of numeric fraud settings, the
 * value shows, and an edit is reported to the screen.
 */
import { describe, expect, it } from 'vitest'
import FraudNumberInput from '@/components/fraud/FraudNumberInput.vue'
import { FraudSettingsField } from '@/types/fraud'
import { mountWithApp } from '../../helpers/mount'

describe('FraudNumberInput', () => {
  it('shows the value under the test id of its setting, with the setting\'s lowest value as the placeholder', () => {
    const wrapper = mountWithApp(FraudNumberInput, { props: { field: FraudSettingsField.VelocityWindowMinutes, modelValue: 60 } })
    const input = wrapper.get('[data-testid="fraud-settings-velocityWindowMinutes"] input')

    expect((input.element as HTMLInputElement).value).toBe('60')
    expect(input.attributes('placeholder')).toBe('1')
  })

  it('writes the unit of the setting inside the input', () => {
    const minutes = mountWithApp(FraudNumberInput, { props: { field: FraudSettingsField.VelocityWindowMinutes, modelValue: 60 } })
    const score = mountWithApp(FraudNumberInput, { props: { field: FraudSettingsField.AutoRejectRiskScore, modelValue: 80 } })

    expect(minutes.get('[data-testid="number-suffix"]').text()).toBe('min')
    expect(score.get('[data-testid="number-suffix"]').text()).toBe('pts')
  })

  it('turns the input off when the screen says protection is off', () => {
    const wrapper = mountWithApp(FraudNumberInput, { props: { field: FraudSettingsField.MaxAccountsPerIp, modelValue: 3, disabled: true } })

    expect(wrapper.get('input').attributes('disabled')).toBeDefined()
  })

  it('reports an edited number', async () => {
    const wrapper = mountWithApp(FraudNumberInput, { props: { field: FraudSettingsField.ManualReviewRiskScore, modelValue: 50 } })

    await wrapper.get('input').setValue('70')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual([70])
  })

  it('marks the input invalid when the form hands over a refusal', () => {
    const wrapper = mountWithApp(FraudNumberInput, {
      props: { field: FraudSettingsField.MaxAccountsPerIp, modelValue: 3, invalid: true, id: 'x', describedby: 'x-message' },
    })

    expect(wrapper.get('input').attributes('aria-invalid')).toBe('true')
    expect(wrapper.get('input').attributes('id')).toBe('x')
  })
})
