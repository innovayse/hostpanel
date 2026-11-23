/**
 * Unit tests for UiSelect: the trigger label, opening the listbox, choosing an option, the empty
 * list, the disabled state and the error line.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiSelect from '~/components/ui/UiSelect.vue'

/** Options used by every test. */
const OPTIONS = [
  { value: 'a', label: 'Alpha' },
  { value: 'b', label: 'Beta' },
]

describe('UiSelect', () => {
  it('shows the placeholder while nothing is selected', () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, placeholder: 'Pick one' } })

    expect(wrapper.find('button').text()).toBe('Pick one')
  })

  it('falls back to the translated default placeholder', () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS } })

    expect(wrapper.find('button').text()).toBe('ui.selectPlaceholder')
  })

  it('shows the label of the selected option', () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, modelValue: 'b', placeholder: 'Pick' } })

    expect(wrapper.find('button').text()).toBe('Beta')
  })

  it('opens the listbox on click and lists the options', async () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, placeholder: 'Pick' } })
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)

    await wrapper.find('button').trigger('click')

    expect(wrapper.find('button').attributes('aria-expanded')).toBe('true')
    expect(wrapper.findAll('[role="option"]')).toHaveLength(2)
  })

  it('emits the chosen value and closes', async () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, placeholder: 'Pick' } })
    await wrapper.find('button').trigger('click')

    await wrapper.find('[data-testid="ui-select-option-b"]').trigger('click')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['b'])
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('closes on Escape', async () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, placeholder: 'Pick' } })
    await wrapper.find('button').trigger('click')

    await wrapper.find('button').trigger('keydown', { key: 'Escape' })

    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('shows the empty message when there are no options', async () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: [], placeholder: 'Pick' } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.find('[role="listbox"]').text()).toBe('ui.noOptions')
  })

  it('does not open when disabled', async () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, disabled: true, placeholder: 'Pick' } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })

  it('binds the label, shows the error and passes the testid', () => {
    const wrapper = mountWithKit(UiSelect, {
      props: { options: OPTIONS, label: 'Plan', error: 'Required', testid: 'plan-select', placeholder: 'Pick' },
    })

    expect(wrapper.find('label').attributes('for')).toBe(wrapper.find('button').attributes('id'))
    expect(wrapper.text()).toContain('Required')
    expect(wrapper.find('button').attributes('data-testid')).toBe('plan-select')
    expect(wrapper.find('button').attributes('aria-invalid')).toBe('true')
  })

  it('scrolls a long list inside its own height instead of running off the screen', async () => {
    const wrapper = mountWithKit(UiSelect, { props: { options: OPTIONS, placeholder: 'Pick' } })

    await wrapper.find('button').trigger('click')

    expect(wrapper.find('[role="listbox"]').classes()).toEqual(expect.arrayContaining(['max-h-60', 'overflow-y-auto']))
  })
})
