/**
 * Unit tests for OtpInput: the cells, typing, completion, Backspace, paste, the error state and
 * the exposed focus method.
 */
import { describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import OtpInput from '~/components/ui/UiOtpInput.vue'

describe('OtpInput', () => {
  it('renders six cells by default and as many as length asks', () => {
    expect(mountWithKit(OtpInput, { props: { modelValue: '' } }).findAll('input')).toHaveLength(6)
    expect(mountWithKit(OtpInput, { props: { modelValue: '', length: 4 } }).findAll('input')).toHaveLength(4)
  })

  it('spreads the value over the cells', () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '123' } })

    const values = wrapper.findAll('input').map((cell) => (cell.element as HTMLInputElement).value)
    expect(values).toEqual(['1', '2', '3', '', '', ''])
  })

  it('emits the code with a typed digit and drops non-digits', async () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '' } })

    await wrapper.find('[data-testid="otp-input-0"]').setValue('a5')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['5'])
  })

  it('emits complete when the last cell is filled', async () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '12345' } })

    await wrapper.find('[data-testid="otp-input-5"]').setValue('6')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['123456'])
    expect(wrapper.emitted('complete')?.[0]).toEqual(['123456'])
  })

  it('does not emit complete while cells are empty', async () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '' } })

    await wrapper.find('[data-testid="otp-input-0"]').setValue('1')

    expect(wrapper.emitted('complete')).toBeUndefined()
  })

  it('clears the current cell on Backspace', async () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '123' } })

    await wrapper.find('[data-testid="otp-input-2"]').trigger('keydown', { key: 'Backspace' })

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['12'])
  })

  it('clears the previous cell on Backspace in an empty cell', async () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '12' } })

    await wrapper.find('[data-testid="otp-input-2"]').trigger('keydown', { key: 'Backspace' })

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['1'])
  })

  it('fills the cells from a pasted code', () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '' } })
    const event = new Event('paste', { bubbles: true, cancelable: true })
    Object.defineProperty(event, 'clipboardData', { value: { getData: () => '98-76 54' } })

    wrapper.find('[data-testid="otp-input-0"]').element.dispatchEvent(event)

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['987654'])
    expect(wrapper.emitted('complete')?.[0]).toEqual(['987654'])
  })

  it('shows the error and the label', () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '', error: 'Wrong code', label: 'Enter code' } })

    expect(wrapper.text()).toContain('Wrong code')
    expect(wrapper.text()).toContain('Enter code')
    expect(wrapper.find('[data-testid="otp-input-0"]').classes()).toContain('border-red-400')
  })

  it('draws a focus ring on every cell state, not only a scale', () => {
    const empty = mountWithKit(OtpInput, { props: { modelValue: '' } })
    const filled = mountWithKit(OtpInput, { props: { modelValue: '1' } })
    const failed = mountWithKit(OtpInput, { props: { modelValue: '', error: 'Wrong code' } })

    for (const cell of [empty.find('input'), filled.find('input'), failed.find('input')]) {
      expect(cell.classes()).toContain('focus-visible:ring-2')
    }
    expect(failed.find('input').classes()).toContain('focus-visible:ring-red-500/50')
  })

  it('disables every cell', () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '', disabled: true } })

    expect(wrapper.findAll('input').every((cell) => cell.attributes('disabled') !== undefined)).toBe(true)
  })

  it('focuses the first empty cell through the exposed method', async () => {
    const wrapper = mountWithKit(OtpInput, { props: { modelValue: '12' }, attachToBody: true })

    ;(wrapper.vm as unknown as { focus: () => void }).focus()
    await nextTick()
    await nextTick()

    expect(document.activeElement).toBe(wrapper.find('[data-testid="otp-input-2"]').element)
    wrapper.unmount()
  })

  it('shrinks its cells on a phone so six of them fit a 360px screen', () => {
    const cell = mountWithKit(OtpInput, { props: { modelValue: '' } }).find('input')

    expect(cell.classes()).toEqual(expect.arrayContaining(['w-10', 'sm:w-12']))
  })
})
