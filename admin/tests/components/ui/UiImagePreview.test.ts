/**
 * Unit tests for UiImagePreview: the picture sits on a light checkerboard by default and the flip button switches it
 * to a dark one and back, naming the stage it switches to.
 */
import { describe, expect, it } from 'vitest'
import { config, mount } from '@vue/test-utils'
import UiImagePreview from '@/components/ui/UiImagePreview.vue'
import { createTestI18n } from '../../helpers/i18n'

config.global.plugins = [createTestI18n()]

describe('UiImagePreview', () => {
  it('draws the picture contained, on a light checkerboard', () => {
    const wrapper = mount(UiImagePreview, { props: { src: '/logo.svg', alt: 'Logo' } })

    expect(wrapper.classes()).toContain('checkerboard-light')
    expect(wrapper.get('img').attributes('src')).toBe('/logo.svg')
    expect(wrapper.get('img').attributes('alt')).toBe('Logo')
    expect(wrapper.get('img').classes()).toContain('object-contain')
  })

  it('takes the test id of the picture and the classes of the box from the caller', () => {
    const wrapper = mount(UiImagePreview, { props: { src: '/logo.svg', alt: 'Logo', imageTestid: 'logo-preview' }, attrs: { class: 'h-20 w-32' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-20', 'w-32']))
    expect(wrapper.get('[data-testid="logo-preview"]').element.tagName).toBe('IMG')
  })

  it('flips between the light and the dark checkerboard', async () => {
    const wrapper = mount(UiImagePreview, { props: { src: '/logo.svg', alt: 'Logo' } })
    const flip = wrapper.get('[data-testid="ui-image-preview-flip"]')

    expect(flip.attributes('aria-label')).toBe('Show on a dark background')
    await flip.trigger('click')

    expect(wrapper.classes()).toContain('checkerboard-dark')
    expect(wrapper.classes()).not.toContain('checkerboard-light')
    expect(flip.attributes('aria-label')).toBe('Show on a light background')

    await flip.trigger('click')

    expect(wrapper.classes()).toContain('checkerboard-light')
  })
})
