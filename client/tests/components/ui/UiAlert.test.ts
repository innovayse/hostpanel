/**
 * Unit tests for UiAlert: the live-region role, the meaning variants, the title, the body and the action slot.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiAlert from '~/components/ui/UiAlert.vue'
import { UiAlertVariant } from '~/utils/ui/enums'

describe('UiAlert', () => {
  it('announces itself as an alert and renders the body', () => {
    const wrapper = mountWithKit(UiAlert, { slots: { default: 'Something happened' } })

    expect(wrapper.attributes('role')).toBe('alert')
    expect(wrapper.text()).toContain('Something happened')
  })

  it.each([
    [UiAlertVariant.Warning, 'alert'],
    [UiAlertVariant.Error, 'alert'],
    [UiAlertVariant.Info, 'status'],
    [UiAlertVariant.Success, 'status'],
  ])('announces the %s variant with role %s', (variant, expected) => {
    const wrapper = mountWithKit(UiAlert, { props: { variant } })

    expect(wrapper.attributes('role')).toBe(expected)
  })

  it('carries no live-region role when it is static content', () => {
    const wrapper = mountWithKit(UiAlert, { props: { variant: UiAlertVariant.Error, live: false } })

    expect(wrapper.attributes('role')).toBeUndefined()
  })

  it('defaults to the warning look', () => {
    const wrapper = mountWithKit(UiAlert)

    expect(wrapper.classes()).toContain('bg-yellow-50')
  })

  it.each([
    [UiAlertVariant.Error, 'bg-red-50'],
    [UiAlertVariant.Info, 'bg-blue-50'],
    [UiAlertVariant.Success, 'bg-green-50'],
  ])('paints the %s variant with %s', (variant, expected) => {
    const wrapper = mountWithKit(UiAlert, { props: { variant } })

    expect(wrapper.classes()).toContain(expected)
  })

  it('renders the title above the body', () => {
    const wrapper = mountWithKit(UiAlert, { props: { title: 'Heads up' }, slots: { default: 'Body' } })

    expect(wrapper.find('p').text()).toBe('Heads up')
  })

  it('renders the action slot', () => {
    const wrapper = mountWithKit(UiAlert, { slots: { action: 'Retry' } })

    expect(wrapper.text()).toContain('Retry')
  })

  it('draws the leading icon at the requested size', () => {
    const wrapper = mountWithKit(UiAlert, { props: { iconSize: 24 } })

    expect(wrapper.find('svg').attributes('width')).toBe('24')
  })

  it('lets the banner wrap and drops the action under the text on a phone', () => {
    const wrapper = mountWithKit(UiAlert, { slots: { action: 'Retry' } })

    expect(wrapper.classes()).toContain('flex-wrap')
    expect(wrapper.find('div.max-sm\\:w-full').text()).toBe('Retry')
  })

  it('draws no action wrapper without the slot', () => {
    const wrapper = mountWithKit(UiAlert)

    expect(wrapper.find('div.max-sm\\:w-full').exists()).toBe(false)
  })
})
