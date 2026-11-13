/**
 * Tests for SettingsFormFooter: Cancel and Save events, the save label and the saving state.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SettingsFormFooter from '@/components/settings/SettingsFormFooter.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the footer with the English catalogue.
 *
 * @param props - Props for the footer.
 * @returns The wrapper.
 */
const mountFooter = (props: { saveLabel: string; saving: boolean; status?: string; cancelLabel?: string; saveLabelCompact?: string; cancelLabelCompact?: string; dirty?: boolean }) =>
  mount(SettingsFormFooter, { props, global: { plugins: [createTestI18n()] } })

describe('SettingsFormFooter', () => {
  it('shows Cancel and the given save label', () => {
    const wrapper = mountFooter({ saveLabel: 'Create slide', saving: false })

    expect(wrapper.get('[data-testid="settings-form-cancel"]').text()).toBe('Cancel')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create slide')
  })

  it('sticks to the content column instead of covering the sidebar', () => {
    const wrapper = mountFooter({ saveLabel: 'Save', saving: false })

    expect(wrapper.classes()).toContain('sticky')
    expect(wrapper.classes()).not.toContain('fixed')
  })

  it('emits cancel and save from their buttons', async () => {
    const wrapper = mountFooter({ saveLabel: 'Save', saving: false })

    await wrapper.get('[data-testid="settings-form-cancel"]').trigger('click')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
    expect(wrapper.emitted('save')).toHaveLength(1)
  })

  it('shows the status line and the custom cancel label when given', () => {
    const wrapper = mountFooter({ saveLabel: 'Save', saving: false, status: '2 unsaved changes', cancelLabel: 'Discard' })

    expect(wrapper.get('[data-testid="settings-form-status"]').text()).toBe('2 unsaved changes')
    expect(wrapper.get('[data-testid="settings-form-cancel"]').text()).toBe('Discard')
  })

  it('shows no status line by default', () => {
    const wrapper = mountFooter({ saveLabel: 'Save', saving: false })

    expect(wrapper.find('[data-testid="settings-form-status"]').exists()).toBe(false)
  })

  it('disables both buttons when there is nothing to save', () => {
    const wrapper = mountFooter({ saveLabel: 'Save', saving: false, dirty: false })

    expect(wrapper.get('[data-testid="settings-form-cancel"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
  })

  it('keeps the Save label, marks it busy and disables it while saving', () => {
    const wrapper = mountFooter({ saveLabel: 'Save', saving: true })
    const save = wrapper.get('[data-testid="settings-form-save"]')

    expect(save.text()).toBe('Save')
    expect(save.attributes('aria-busy')).toBe('true')
    expect(save.attributes('disabled')).toBeDefined()
  })

  it('stays one compact row on a phone: tight padding, 32px buttons that do not wrap, a status line that truncates', () => {
    const wrapper = mountFooter({ saveLabel: 'Save changes', saving: false, status: '2 unsaved changes' })
    const save = wrapper.get('[data-testid="settings-form-save"]')

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['px-3', 'py-2', 'sm:px-6', 'sm:py-4']))
    expect(save.classes()).toEqual(expect.arrayContaining(['whitespace-nowrap', 'max-sm:h-8', 'max-sm:px-3']))
    expect(wrapper.get('[data-testid="settings-form-status"]').classes()).toContain('truncate')
  })

  it('draws the short labels for a phone beside the full ones when the screen gives them', () => {
    const wrapper = mountFooter({
      saveLabel: 'Save changes',
      saving: false,
      cancelLabel: 'Discard changes',
      saveLabelCompact: 'Save',
      cancelLabelCompact: 'Discard',
    })
    const save = wrapper.get('[data-testid="settings-form-save"]')
    const cancel = wrapper.get('[data-testid="settings-form-cancel"]')

    /**
     * Reads the label spans of a button.
     *
     * @param button - The button.
     * @returns Each span's first class and text.
     */
    const spans = (button: typeof save): (string | undefined)[][] => button.findAll('span').map(span => [span.classes()[0], span.text()])

    expect(spans(save)).toEqual([['sm:hidden', 'Save'], ['max-sm:hidden', 'Save changes']])
    expect(spans(cancel)).toEqual([['sm:hidden', 'Discard'], ['max-sm:hidden', 'Discard changes']])
  })

  it('keeps the short and the full label while saving', () => {
    const wrapper = mountFooter({ saveLabel: 'Save changes', saving: true, saveLabelCompact: 'Save' })

    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('SaveSave changes')
  })
})
