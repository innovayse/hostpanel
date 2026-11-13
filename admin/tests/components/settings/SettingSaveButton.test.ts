/**
 * Tests for SettingSaveButton: disabled until there is an edit, one click event, saving label.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the button with the English catalogue.
 *
 * @param props - Props for the button.
 * @returns The wrapper.
 */
const mountButton = (props: { dirty: boolean; saving?: boolean }) =>
  mount(SettingSaveButton, { props, global: { plugins: [createTestI18n()] } })

describe('SettingSaveButton', () => {
  it('is disabled while there is nothing to save', () => {
    const wrapper = mountButton({ dirty: false })

    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toBe('Save')
  })

  it('emits click once when pressed with an edit pending', async () => {
    const wrapper = mountButton({ dirty: true })

    await wrapper.get('button').trigger('click')

    expect(wrapper.emitted('click')).toHaveLength(1)
  })

  it('keeps the label, marks it busy and blocks the click while a save is in flight', async () => {
    const wrapper = mountButton({ dirty: true, saving: true })

    expect(wrapper.text()).toBe('Save')
    expect(wrapper.get('button').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('click')).toBeUndefined()
  })
})
