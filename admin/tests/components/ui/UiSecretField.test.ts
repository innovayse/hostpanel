/**
 * Mount tests for UiSecretField: a stored secret shows as a read-only box with the Set badge and small Replace
 * and Clear buttons, Replace opens an empty password input, Cancel drops what was typed, Clear and its pending
 * state ask the parent, a multi-line secret opens a text area, and nothing stored is an empty input from the start.
 */
import { describe, expect, it } from 'vitest'
import UiSecretField from '@/components/ui/UiSecretField.vue'
import { mountWithApp } from '../../helpers/mount'

/**
 * Mounts the control.
 *
 * @param props - Props to change from a stored, untouched secret.
 * @returns The wrapper.
 */
const mountField = (props: Record<string, unknown> = {}) =>
  mountWithApp(UiSecretField, { props: { hasValue: true, modelValue: '', testid: 'secret', ...props } })

describe('UiSecretField', () => {
  it('shows a stored secret as a masked box with the Set badge, Replace and Clear, and no input', () => {
    const wrapper = mountField()

    const box = wrapper.get('[data-testid="secret-masked"]')
    expect(box.text()).toContain('Set')
    expect(box.find('svg').exists()).toBe(true)
    expect(box.classes()).toContain('h-10.5')
    expect(wrapper.find('[data-testid="secret-input"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="secret-replace"]').classes()).toContain('h-8')
    expect(wrapper.get('[data-testid="secret-clear"]').classes()).toContain('h-8')
  })

  it('opens an empty password input on Replace and emits what is typed', async () => {
    const wrapper = mountField()

    await wrapper.get('[data-testid="secret-replace"]').trigger('click')
    const input = wrapper.get('[data-testid="secret-input"]')
    expect(input.attributes('type')).toBe('password')
    expect((input.element as HTMLInputElement).value).toBe('')
    await input.setValue('new-secret')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual(['new-secret'])
  })

  it('drops the typed value and shows the mask again on Cancel', async () => {
    const wrapper = mountField()

    await wrapper.get('[data-testid="secret-replace"]').trigger('click')
    await wrapper.get('[data-testid="secret-cancel"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual([''])
    expect(wrapper.find('[data-testid="secret-masked"]').exists()).toBe(true)
  })

  it('emits clear from the Clear button', async () => {
    const wrapper = mountField()

    await wrapper.get('[data-testid="secret-clear"]').trigger('click')

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })

  it('shows the pending clear with Cancel, which emits cancelClear, while the parent reports it cleared', async () => {
    const wrapper = mountField({ cleared: true })

    expect(wrapper.get('[data-testid="secret-pending-clear"]').text()).toBe('Cleared when you save')
    expect(wrapper.find('[data-testid="secret-masked"]').exists()).toBe(false)
    await wrapper.get('[data-testid="secret-cancel-clear"]').trigger('click')

    expect(wrapper.emitted('cancelClear')).toHaveLength(1)
  })

  it('opens a text area on Replace for a multi-line secret', async () => {
    const wrapper = mountField({ multiline: true })

    await wrapper.get('[data-testid="secret-replace"]').trigger('click')
    await wrapper.get('textarea[data-testid="secret-input"]').setValue('line one')

    expect(wrapper.emitted('update:modelValue')?.at(-1)).toEqual(['line one'])
  })

  it('shows an empty password input and no Cancel when nothing is stored', () => {
    const wrapper = mountField({ hasValue: false })

    const input = wrapper.get('[data-testid="secret-input"]')
    expect(input.attributes('type')).toBe('password')
    expect(input.attributes('placeholder')).toBe('Not set')
    expect(wrapper.find('[data-testid="secret-cancel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="secret-masked"]').exists()).toBe(false)
  })

  it('blocks every action while disabled', () => {
    const wrapper = mountField({ disabled: true })

    expect(wrapper.get('[data-testid="secret-replace"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="secret-clear"]').attributes('disabled')).toBeDefined()
  })
})
