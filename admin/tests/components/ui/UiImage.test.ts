/**
 * Unit tests for UiImage: native img, decorative by default, fit and radius.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiImage from '@/components/ui/UiImage.vue'

describe('UiImage', () => {
  it('renders a native img carrying the given attributes', () => {
    const wrapper = mount(UiImage, { props: { src: '/a.png', alt: 'logo' }, attrs: { class: 'w-4' } })

    expect(wrapper.element.tagName).toBe('IMG')
    expect(wrapper.attributes('src')).toBe('/a.png')
    expect(wrapper.attributes('alt')).toBe('logo')
    expect(wrapper.classes()).toContain('w-4')
  })

  it('draws a framed icon box of the same size and radius when it has no src and a fallback is asked for', () => {
    const wrapper = mount(UiImage, { props: { fallback: true, rounded: 'md' }, attrs: { class: 'h-10 w-16' } })

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.attributes('data-testid')).toBe('ui-image-fallback')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-10', 'w-16', 'rounded-control', 'bg-surface-field']))
    expect(wrapper.find('svg').exists()).toBe(true)
  })

  it('keeps the picture when it has a src, fallback or not', () => {
    expect(mount(UiImage, { props: { src: '/a.png', fallback: true } }).element.tagName).toBe('IMG')
  })

  it('is decorative unless it has alternative text', () => {
    expect(mount(UiImage, { props: { src: '/a.png' } }).attributes('alt')).toBe('')
  })

  it('applies fit and radius', () => {
    const wrapper = mount(UiImage, { props: { src: '/a.png', fit: 'cover', rounded: 'full' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['object-cover', 'rounded-full']))
  })

  it('maps the radius steps to the control and card radii', () => {
    expect(mount(UiImage, { props: { src: '/a.png', rounded: 'md' } }).classes()).toContain('rounded-control')
    expect(mount(UiImage, { props: { src: '/a.png', rounded: 'lg' } }).classes()).toContain('rounded-card')
  })
})
