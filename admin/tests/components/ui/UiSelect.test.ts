/**
 * Unit tests for UiSelect: trigger label, ARIA roles, opening, picking, searching, the keyboard
 * contract, the empty and loading rows, remote lookups and the compact look for toolbars. The teleport is inlined
 * unless a test says otherwise.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'

/** Options used by the tests. */
const options: UiSelectOption<string>[] = [
  { value: 'a', label: 'Alpha' },
  { value: 'b', label: 'Beta' },
  { value: 'c', label: 'Bravo' },
]

/** Props the tests vary. */
interface SelectProps {
  /** The selected value. */
  modelValue: string
  /** Trigger hint. */
  placeholder?: string
  /** Disables the control. */
  disabled?: boolean
  /** Adds the filter field. */
  searchable?: boolean
  /** Small toolbar look. */
  compact?: boolean
  /** Joined to a field on its right. */
  attached?: boolean
  /** Accessible name of the trigger. */
  ariaLabel?: string
  /** Options, defaulting to the shared list. */
  options?: UiSelectOption<string>[]
  /** Owner does the matching. */
  remote?: boolean
  /** Owner is fetching. */
  loading?: boolean
  /** Text of the empty row. */
  emptyLabel?: string
  /** Label of a value outside the options. */
  valueLabel?: string
}

/**
 * Mounts the select attached to the document (focus and outside clicks need it).
 *
 * @param props - Props for the select.
 * @returns The wrapper.
 */
const mountSelect = (props: SelectProps) =>
  mount(UiSelect, { props: { options, ...props }, attachTo: document.body, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

/**
 * Presses a key on the element a reader's focus would be on.
 *
 * @param wrapper - The mounted select.
 * @param key - The `KeyboardEvent.key` value.
 * @param target - Selector of the element receiving the key; the trigger by default.
 * @returns Resolves once the DOM updated.
 */
const press = async (wrapper: VueWrapper, key: string, target = 'button'): Promise<void> => {
  await wrapper.get(target).trigger('keydown', { key })
}

/** The mounted wrappers, unmounted after each test so their document listeners go. */
const mounted: VueWrapper[] = []

/**
 * Mounts and remembers the wrapper for cleanup.
 *
 * @param props - Props for the select.
 * @returns The wrapper.
 */
const track = (props: SelectProps): VueWrapper => {
  const wrapper = mountSelect(props)
  mounted.push(wrapper)
  return wrapper
}

afterEach(() => {
  vi.useRealTimers()
  mounted.splice(0).forEach(wrapper => wrapper.unmount())
})

describe('UiSelect', () => {
  it('shows the label of the selected option', () => {
    expect(track({ modelValue: 'b' }).get('button').text()).toBe('Beta')
  })

  it('shows the placeholder in the muted colour while nothing matches', () => {
    const wrapper = track({ modelValue: '', placeholder: 'Pick one' })

    expect(wrapper.get('button').text()).toBe('Pick one')
    expect(wrapper.get('button span').classes()).toContain('text-text-muted')
  })

  it('draws a chosen label in the primary colour even when it equals the placeholder', () => {
    const wrapper = track({ modelValue: 'a', placeholder: 'Alpha' })

    expect(wrapper.get('button span').classes()).toContain('text-text-primary')
    expect(wrapper.get('button span').classes()).not.toContain('text-text-muted')
  })

  it('draws a dim choice dim on the trigger and shows a value label that is not among the options', () => {
    const dim = track({ modelValue: '', options: [{ value: '', label: 'All', muted: true }] })
    const outside = track({ modelValue: 'zz', valueLabel: 'Loaded elsewhere' })

    expect(dim.get('button span').classes()).toContain('text-text-muted')
    expect(outside.get('button').text()).toBe('Loaded elsewhere')
    expect(outside.get('button span').classes()).toContain('text-text-primary')
  })

  describe('roles', () => {
    it('makes a plain trigger the combobox that owns the listbox', async () => {
      const wrapper = track({ modelValue: 'a' })
      const trigger = wrapper.get('button')

      expect(trigger.attributes('role')).toBe('combobox')
      expect(trigger.attributes('aria-haspopup')).toBe('listbox')
      expect(trigger.attributes('aria-expanded')).toBe('false')
      expect(trigger.attributes('aria-controls')).toBeUndefined()

      await trigger.trigger('click')

      expect(trigger.attributes('aria-expanded')).toBe('true')
      expect(trigger.attributes('aria-controls')).toBe(wrapper.get('[role="listbox"]').attributes('id'))
      expect(wrapper.findAll('[role="option"]')).toHaveLength(3)
    })

    it('makes the filter field the combobox when searchable, and the trigger only its opener', async () => {
      const wrapper = track({ modelValue: 'a', searchable: true })

      expect(wrapper.get('button').attributes('role')).toBeUndefined()
      await wrapper.get('button').trigger('click')
      const input = wrapper.get('input')

      expect(input.attributes('role')).toBe('combobox')
      expect(input.attributes('aria-autocomplete')).toBe('list')
      expect(input.attributes('aria-controls')).toBe(wrapper.get('[role="listbox"]').attributes('id'))
    })

    it('puts an id on every option and names the highlighted one in aria-activedescendant', async () => {
      const wrapper = track({ modelValue: 'b' })

      await wrapper.get('button').trigger('click')
      const rows = wrapper.findAll('[role="option"]')

      expect(new Set(rows.map(row => row.attributes('id'))).size).toBe(3)
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(rows[1]?.attributes('id'))
    })
  })

  it('opens the list on click and marks the selected option', async () => {
    const wrapper = track({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    const items = wrapper.findAll('[role="option"]')

    expect(wrapper.find('[role="listbox"]').exists()).toBe(true)
    expect(items[0]?.attributes('aria-selected')).toBe('true')
    expect(items[1]?.attributes('aria-selected')).toBe('false')
  })

  it('paints the selected option solid from the tokens and the highlighted one softly', async () => {
    const wrapper = track({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    await wrapper.findAll('[role="option"]')[1]?.trigger('mousemove')
    const items = wrapper.findAll('[role="option"]')

    expect(items[0]?.classes()).toEqual(expect.arrayContaining(['bg-primary-400', 'text-surface-base']))
    expect(items[1]?.classes()).not.toContain('bg-primary-400')
    expect(items[1]?.classes()).toContain('bg-surface-hover')
  })

  it('emits the picked value, closes and remembers the choice', async () => {
    const wrapper = track({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    await wrapper.findAll('[role="option"]')[1]?.trigger('mousedown')

    expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['b'])
    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('renders caption and description lines of a rich option', async () => {
    const wrapper = track({
      modelValue: '',
      options: [{ value: 'x', label: 'Acme', displayLabel: 'Acme - #1', caption: 'Active', description: 'a@b.test' }],
    })

    await wrapper.get('button').trigger('click')

    expect(wrapper.get('[role="option"]').text()).toBe('ActiveAcme - #1a@b.test')
  })

  describe('search', () => {
    it('filters by the search term and reports no results', async () => {
      const wrapper = track({ modelValue: 'a', searchable: true })

      await wrapper.get('button').trigger('click')
      const search = wrapper.get('input')
      await search.setValue('bet')

      expect(wrapper.findAll('[role="option"]')).toHaveLength(1)

      await search.setValue('zzz')

      expect(wrapper.findAll('[role="option"]')).toHaveLength(0)
      expect(wrapper.get('[data-testid="ui-select-empty"]').text()).toBe('No results')
    })

    it('says what the caller wants in the empty row', async () => {
      const wrapper = track({ modelValue: 'a', options: [], emptyLabel: 'Nothing here' })

      await wrapper.get('button').trigger('click')

      expect(wrapper.get('[data-testid="ui-select-empty"]').text()).toBe('Nothing here')
    })

    it('focuses the filter field when the list opens and clears the term on close', async () => {
      const wrapper = track({ modelValue: 'a', searchable: true })

      await wrapper.get('button').trigger('click')
      await wrapper.vm.$nextTick()
      expect(document.activeElement).toBe(wrapper.get('input').element)

      await wrapper.get('input').setValue('bet')
      await press(wrapper, 'Escape', 'input')
      await wrapper.get('button').trigger('click')

      expect((wrapper.get('input').element as HTMLInputElement).value).toBe('')
    })

    it('opens with the typed character as the first search character', async () => {
      const wrapper = track({ modelValue: 'a', searchable: true })

      await press(wrapper, 'b')
      await wrapper.vm.$nextTick()

      expect((wrapper.get('input').element as HTMLInputElement).value).toBe('b')
      expect(wrapper.findAll('[role="option"]')).toHaveLength(2)
    })
  })

  describe('keyboard', () => {
    it('opens on ArrowDown with the chosen row highlighted', async () => {
      const wrapper = track({ modelValue: 'b' })

      await press(wrapper, 'ArrowDown')

      expect(wrapper.find('[role="listbox"]').exists()).toBe(true)
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(wrapper.findAll('[role="option"]')[1]?.attributes('id'))
    })

    it('moves the highlight with the arrows, Home and End and stops at the ends', async () => {
      const wrapper = track({ modelValue: 'a' })
      await press(wrapper, 'ArrowDown')
      const ids = wrapper.findAll('[role="option"]').map(row => row.attributes('id'))

      await press(wrapper, 'ArrowDown')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[1])
      await press(wrapper, 'End')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[2])
      await press(wrapper, 'ArrowDown')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[2])
      await press(wrapper, 'Home')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[0])
      await press(wrapper, 'ArrowUp')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[0])
    })

    it('picks the highlighted row on Enter', async () => {
      const wrapper = track({ modelValue: 'a' })
      await press(wrapper, 'ArrowDown')
      await press(wrapper, 'ArrowDown')

      await press(wrapper, 'Enter')

      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['b'])
      expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
    })

    it('closes on Escape and gives the focus back to the trigger', async () => {
      const wrapper = track({ modelValue: 'a' })
      await press(wrapper, 'ArrowDown')

      await press(wrapper, 'Escape')

      expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
      expect(document.activeElement).toBe(wrapper.get('button').element)
    })

    it('closes on Tab without picking', async () => {
      const wrapper = track({ modelValue: 'a' })
      await press(wrapper, 'ArrowDown')

      await press(wrapper, 'Tab')

      expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
      expect(wrapper.emitted('update:modelValue')).toBeUndefined()
    })

    it('jumps to the label a typed word starts', async () => {
      const wrapper = track({ modelValue: 'a' })
      await press(wrapper, 'ArrowDown')
      const ids = wrapper.findAll('[role="option"]').map(row => row.attributes('id'))

      await press(wrapper, 'b')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[1])
      await press(wrapper, 'r')
      expect(wrapper.get('button').attributes('aria-activedescendant')).toBe(ids[2])
    })

    it('picks the first match on Enter in the filter field and ignores Home and End there', async () => {
      const wrapper = track({ modelValue: 'a', searchable: true })
      await wrapper.get('button').trigger('click')
      await wrapper.get('input').setValue('br')

      await press(wrapper, 'Home', 'input')
      await press(wrapper, 'Enter', 'input')

      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['c'])
    })

    it('moves the highlight inside the filter field with the arrows', async () => {
      const wrapper = track({ modelValue: 'a', searchable: true })
      await wrapper.get('button').trigger('click')
      await wrapper.get('input').setValue('b')

      await press(wrapper, 'ArrowDown', 'input')
      await press(wrapper, 'Enter', 'input')

      expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['c'])
    })

    it('does nothing while disabled', async () => {
      const wrapper = track({ modelValue: 'a', disabled: true })

      await press(wrapper, 'ArrowDown')

      expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
    })
  })

  describe('remote lookup', () => {
    it('asks for the first matches on open and shows what the owner sends without filtering it', async () => {
      const wrapper = track({ modelValue: '', remote: true, options })

      await wrapper.get('button').trigger('click')
      await wrapper.get('input').setValue('zzz')

      expect(wrapper.emitted('search')?.[0]).toEqual([''])
      expect(wrapper.findAll('[role="option"]')).toHaveLength(3)
    })

    it('sends the typed term once typing pauses', async () => {
      vi.useFakeTimers()
      const wrapper = track({ modelValue: '', remote: true })
      await wrapper.get('button').trigger('click')

      await wrapper.get('input').setValue('ad')
      await wrapper.get('input').setValue('ada')
      expect(wrapper.emitted('search')).toHaveLength(1)
      vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS)

      expect(wrapper.emitted('search')).toEqual([[''], ['ada']])
    })

    it('drops a pending lookup when the list closes', async () => {
      vi.useFakeTimers()
      const wrapper = track({ modelValue: '', remote: true })
      await wrapper.get('button').trigger('click')
      await wrapper.get('input').setValue('ada')

      await press(wrapper, 'Escape', 'input')
      vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS)

      expect(wrapper.emitted('search')).toEqual([['']])
    })

    it('shows the loading row while the owner fetches, in place of the empty row', async () => {
      const wrapper = track({ modelValue: '', remote: true, options: [], loading: true })

      await wrapper.get('button').trigger('click')

      expect(wrapper.get('[data-testid="ui-select-loading"]').attributes('aria-busy')).toBe('true')
      expect(wrapper.find('[data-testid="ui-select-empty"]').exists()).toBe(false)
    })

    it('keeps the trigger label of the picked option when the owner replaces the options', async () => {
      const wrapper = track({ modelValue: '', remote: true })
      await wrapper.get('button').trigger('click')
      await wrapper.findAll('[role="option"]')[1]?.trigger('mousedown')

      await wrapper.setProps({ modelValue: 'b', options: [{ value: 'z', label: 'Zulu' }] })

      expect(wrapper.get('button').text()).toBe('Beta')
    })

    it('hides the dim "none" row once a term is typed', async () => {
      const wrapper = track({ modelValue: '', remote: true, options: [{ value: '', label: 'All', muted: true }, ...options] })
      await wrapper.get('button').trigger('click')
      expect(wrapper.findAll('[role="option"]')).toHaveLength(4)

      await wrapper.get('input').setValue('b')

      expect(wrapper.findAll('[role="option"]')).toHaveLength(3)
    })
  })

  it('closes when the pointer goes down outside', async () => {
    const wrapper = track({ modelValue: 'a' })

    await wrapper.get('button').trigger('click')
    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('does not open while disabled', async () => {
    const wrapper = track({ modelValue: 'a', disabled: true })

    await wrapper.get('button').trigger('click')

    expect(wrapper.find('[role="listbox"]').exists()).toBe(false)
  })

  it('widens the list on request and keeps data-testid on the root', async () => {
    const wrapper = mount(UiSelect, {
      props: { modelValue: 'a', options, wide: true },
      attrs: { 'data-testid': 'probe' },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    await wrapper.get('button').trigger('click')

    expect(wrapper.attributes('data-testid')).toBe('probe')
    expect(wrapper.get('[data-testid="ui-select-panel"]').classes()).toEqual(expect.arrayContaining(['min-w-[18rem]', 'max-w-[calc(100vw-4rem)]']))
  })

  it('keeps the field look when it is not compact', async () => {
    const wrapper = track({ modelValue: 'a' })

    expect(wrapper.get('button').classes()).toEqual(expect.arrayContaining(['h-10.5', 'px-3.5', 'rounded-control']))
    await wrapper.get('button').trigger('click')
    expect(wrapper.get('[data-testid="ui-select-panel"]').classes()).not.toContain('rounded-xl')
  })

  it('draws a small trigger like a small button when compact', () => {
    const wrapper = track({ modelValue: 'a', compact: true })

    expect(wrapper.get('button').classes()).toEqual(expect.arrayContaining(['h-8', 'text-[0.8125rem]', 'rounded-[9px]']))
    expect(wrapper.get('button').classes()).not.toContain('h-10.5')
  })

  it('opens a soft list when compact: inset rounded rows and a tinted, not solid, selection', async () => {
    const wrapper = track({ modelValue: 'a', compact: true })

    await wrapper.get('button').trigger('click')
    const items = wrapper.findAll('[role="option"]')

    expect(wrapper.get('[data-testid="ui-select-panel"]').classes()).toEqual(expect.arrayContaining(['rounded-xl', 'p-1']))
    expect(items[0]?.classes()).toEqual(expect.arrayContaining(['rounded-lg', 'bg-primary-500/10']))
    expect(items[0]?.classes()).not.toContain('bg-primary-400')
  })

  it('puts the id on the trigger and flags it invalid', () => {
    const wrapper = mount(UiSelect, {
      props: { modelValue: 'a', options, id: 'method', invalid: true },
      global: { plugins: [createTestI18n()], stubs: { teleport: true } },
    })

    expect(wrapper.get('button').attributes('id')).toBe('method')
    expect(wrapper.get('button').attributes('aria-invalid')).toBe('true')
    expect(wrapper.get('button').classes()).toContain('border-status-red/60')
  })

  it('is not flagged invalid by default', () => {
    expect(track({ modelValue: 'a' }).get('button').attributes('aria-invalid')).toBeUndefined()
  })

  it('names the trigger when given an accessible name', () => {
    expect(track({ modelValue: 'a', ariaLabel: 'Pick a letter' }).get('button').attributes('aria-label')).toBe('Pick a letter')
  })

  it('has no accessible name of its own by default', () => {
    expect(track({ modelValue: 'a' }).get('button').attributes('aria-label')).toBeUndefined()
  })

  it('squares the right side of the trigger when attached to a field', () => {
    const classes = track({ modelValue: 'a', attached: true }).get('button').classes()

    expect(classes).toContain('rounded-l-control')
    expect(classes).toContain('border-r-0')
    expect(classes).not.toContain('rounded-control')
  })

  it('keeps the full rounded trigger when not attached', () => {
    expect(track({ modelValue: 'a' }).get('button').classes()).toContain('rounded-control')
  })

  describe('list drawn in the page body', () => {
    /**
     * Mounts the select with the real teleport, so the list leaves the component the way it does in the app.
     *
     * @returns The wrapper.
     */
    const mountTeleported = (): VueWrapper => {
      const wrapper = mount(UiSelect, { props: { modelValue: 'a', options }, attachTo: document.body, global: { plugins: [createTestI18n()] } })
      mounted.push(wrapper)
      return wrapper
    }

    it('puts the list in the body, fixed at its place, so no card around the trigger can paint over it', async () => {
      const wrapper = mountTeleported()

      await wrapper.get('button').trigger('click')

      const panel = document.body.querySelector('[data-testid="ui-select-panel"]')
      expect(panel).not.toBeNull()
      expect(wrapper.element.contains(panel)).toBe(false)
      expect(panel?.classList.contains('fixed')).toBe(true)
      expect(panel?.classList.contains('absolute')).toBe(false)
      expect(panel?.classList.contains('w-[var(--fp-width)]')).toBe(true)
    })

    it('stays open while the pointer goes down inside the list and closes when it goes down elsewhere', async () => {
      const wrapper = mountTeleported()
      await wrapper.get('button').trigger('click')

      document.body.querySelector('[data-testid="ui-select-panel"]')?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
      await nextTick()
      expect(document.body.querySelector('[data-testid="ui-select-panel"]')).not.toBeNull()

      document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
      await nextTick()
      expect(document.body.querySelector('[data-testid="ui-select-panel"]')).toBeNull()
    })

    it('does not let a pointer-down in the list reach an enclosing picker that would close on it', async () => {
      const wrapper = mountTeleported()
      await wrapper.get('button').trigger('click')
      const outer = vi.fn()
      document.body.addEventListener('mousedown', outer)

      document.body.querySelector('[data-testid="ui-select-panel"]')?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))

      expect(outer).not.toHaveBeenCalled()
      document.body.removeEventListener('mousedown', outer)
    })
  })
})
