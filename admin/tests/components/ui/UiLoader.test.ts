/**
 * Unit tests for UiLoader: a busy status region named by its label, a ring that stops for reduced motion, a label
 * that is spoken or shown, and a ring size per size.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiLoader from '@/components/ui/UiLoader.vue'
import { UiSize } from '@/utils/ui/size'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the loader with the translations a screen has.
 *
 * @param props - Props to pass.
 * @returns The mounted wrapper.
 */
const mountLoader = (props: InstanceType<typeof UiLoader>['$props'] = {}) =>
  mount(UiLoader, { props, global: { plugins: [createTestI18n()] } })

describe('UiLoader', () => {
  it('is a busy status region named by its label', () => {
    const wrapper = mountLoader({ label: 'Uploading...' })

    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="ui-loader-label"]').text()).toBe('Uploading...')
  })

  it('names itself with the shared loading label when none is given', () => {
    const wrapper = mountLoader()

    expect(wrapper.get('[data-testid="ui-loader-label"]').text()).toBe('Loading')
    expect(wrapper.get('[data-testid="ui-loader-label"]').classes()).toContain('sr-only')
  })

  it('leaves the label to screen readers until it is asked to show it', async () => {
    const wrapper = mountLoader({ label: 'Loading' })

    expect(wrapper.get('[data-testid="ui-loader-label"]').classes()).toContain('sr-only')

    await wrapper.setProps({ showLabel: true })

    expect(wrapper.get('[data-testid="ui-loader-label"]').classes()).not.toContain('sr-only')
  })

  it('spins the ring, hides it from assistive technology and stops it under reduced motion', () => {
    const ring = mountLoader({ label: 'Loading' }).get('[data-testid="ui-loader-ring"]')

    expect(ring.classes()).toEqual(expect.arrayContaining(['animate-spin', 'motion-reduce:animate-none']))
    expect(ring.attributes('aria-hidden')).toBe('true')
  })

  it('sizes the ring', () => {
    const small = mountLoader({ label: 'Loading' }).get('[data-testid="ui-loader-ring"]')
    const large = mountLoader({ label: 'Loading', size: UiSize.Lg }).get('[data-testid="ui-loader-ring"]')

    expect(small.classes()).toEqual(expect.arrayContaining(['h-4', 'w-4']))
    expect(large.classes()).toEqual(expect.arrayContaining(['h-6', 'w-6']))
  })
})
