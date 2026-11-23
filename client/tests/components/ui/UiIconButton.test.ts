/**
 * Unit tests for UiIconButton: the accessible name, the icon, variants and sizes, the disabled
 * state and the click and testid pass-through.
 */
import { describe, expect, it, vi } from 'vitest'
import { Trash2 } from 'lucide-vue-next'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiIconButton from '~/components/ui/UiIconButton.vue'
import { UiIconButtonVariant, UiSize } from '~/utils/ui/enums'

describe('UiIconButton', () => {
  it('is a type=button button named by its label', () => {
    const wrapper = mountWithKit(UiIconButton, { props: { label: 'Delete' } })

    expect(wrapper.element.tagName).toBe('BUTTON')
    expect(wrapper.attributes('type')).toBe('button')
    expect(wrapper.attributes('aria-label')).toBe('Delete')
    expect(wrapper.attributes('title')).toBe('Delete')
  })

  it('uses a separate title when given', () => {
    const wrapper = mountWithKit(UiIconButton, { props: { label: 'Delete', title: 'Delete this row' } })

    expect(wrapper.attributes('title')).toBe('Delete this row')
    expect(wrapper.attributes('aria-label')).toBe('Delete')
  })

  it('draws the icon component and the slot', () => {
    const withIcon = mountWithKit(UiIconButton, { props: { label: 'Delete', icon: Trash2 } })
    const withSlot = mountWithKit(UiIconButton, { props: { label: 'Delete' }, slots: { default: 'X' } })

    expect(withIcon.find('svg').exists()).toBe(true)
    expect(withSlot.text()).toBe('X')
  })

  it.each([
    [UiIconButtonVariant.Primary, 'bg-primary-600'],
    [UiIconButtonVariant.Danger, 'text-red-600'],
    [UiIconButtonVariant.Ghost, 'border-transparent'],
    [UiIconButtonVariant.Subtle, 'bg-gray-100'],
    [UiIconButtonVariant.Bare, 'rounded-lg'],
  ])('paints the %s variant with %s', (variant, expected) => {
    const wrapper = mountWithKit(UiIconButton, { props: { label: 'x', variant } })

    expect(wrapper.classes()).toContain(expected)
  })

  it('keeps every size at a 44px touch target on a phone', () => {
    for (const size of [UiSize.Sm, UiSize.Md, UiSize.Lg]) {
      expect(mountWithKit(UiIconButton, { props: { label: 'x', size } }).classes()).toEqual(expect.arrayContaining(['w-11', 'h-11']))
    }
  })

  it('maps the sizes to square dimensions', () => {
    expect(mountWithKit(UiIconButton, { props: { label: 'x', size: UiSize.Sm } }).classes()).toContain('sm:w-8')
    expect(mountWithKit(UiIconButton, { props: { label: 'x' } }).classes()).toContain('sm:w-9')
    expect(mountWithKit(UiIconButton, { props: { label: 'x', size: UiSize.Lg } }).classes()).toContain('sm:w-10')
  })

  it('disables the button', () => {
    const wrapper = mountWithKit(UiIconButton, { props: { label: 'x', disabled: true } })

    expect(wrapper.attributes('disabled')).toBeDefined()
  })

  it('passes clicks through and puts the testid on the button', async () => {
    const onClick = vi.fn()
    const wrapper = mountWithKit(UiIconButton, { props: { label: 'x', testid: 'row-delete' }, attrs: { onClick } })

    await wrapper.trigger('click')

    expect(onClick).toHaveBeenCalledTimes(1)
    expect(wrapper.attributes('data-testid')).toBe('row-delete')
  })
})
