/**
 * Unit tests for UiSvgMap: labelled outlines are focusable controls that report hover, focus and choice; unlabelled
 * outlines are decoration.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSvgMap from '@/components/ui/UiSvgMap.vue'
import { mapShapeFocusClasses } from '@/utils/ui/svgMap'
import type { UiMapShape } from '@/utils/ui/svgMap'

/** One decorative outline and one labelled one. */
const shapes: UiMapShape[] = [
  { id: 'a', path: 'M0 0h1v1z', fill: 'fill-border' },
  { id: 'b', path: 'M2 2h1v1z', fill: 'fill-primary-300', label: 'B: 3', testid: 'shape-b' },
]

/**
 * Mounts the map around the shapes.
 *
 * @returns The wrapper.
 */
const mountMap = () => mount(UiSvgMap, { props: { viewBox: '0 0 10 10', label: 'Map', shapes } })

describe('UiSvgMap', () => {
  it('names the whole map and draws every outline with its fill', () => {
    const wrapper = mountMap()

    expect(wrapper.attributes('aria-label')).toBe('Map')
    expect(wrapper.attributes('viewBox')).toBe('0 0 10 10')
    expect(wrapper.findAll('path')).toHaveLength(2)
    expect(wrapper.findAll('path')[0]?.classes()).toContain('fill-border')
  })

  it('hides a decorative outline and makes a labelled one a focusable button', () => {
    const wrapper = mountMap()

    expect(wrapper.findAll('path')[0]?.attributes('aria-hidden')).toBe('true')
    expect(wrapper.get('[data-testid="shape-b"]').attributes('role')).toBe('button')
    expect(wrapper.get('[data-testid="shape-b"]').attributes('tabindex')).toBe('0')
    expect(wrapper.get('[data-testid="shape-b"]').attributes('aria-label')).toBe('B: 3')
  })

  it('shows keyboard focus on a labelled outline as an accent stroke, and keeps a decorative one plain', () => {
    const wrapper = mountMap()
    const labelled = wrapper.get('[data-testid="shape-b"]').classes()

    for (const focusClass of mapShapeFocusClasses.split(' ')) {
      expect(labelled).toContain(focusClass)
    }
    expect(labelled).toContain('focus-visible:stroke-text-accent')
    expect(wrapper.findAll('path')[0]?.classes()).not.toContain('focus-visible:stroke-text-accent')
  })

  it('reports the pointer and focus entering and leaving a labelled outline', async () => {
    const wrapper = mountMap()
    const shape = wrapper.get('[data-testid="shape-b"]')

    await shape.trigger('mouseenter')
    await shape.trigger('mouseleave')
    await shape.trigger('focus')
    await shape.trigger('blur')

    expect(wrapper.emitted('activate')).toEqual([['b'], ['b']])
    expect(wrapper.emitted('deactivate')).toHaveLength(2)
  })

  it('reports a click, Enter and Space on a labelled outline as a choice', async () => {
    const wrapper = mountMap()
    const shape = wrapper.get('[data-testid="shape-b"]')

    await shape.trigger('click')
    await shape.trigger('keydown', { key: 'Enter' })
    await shape.trigger('keydown', { key: ' ' })

    expect(wrapper.emitted('select')).toEqual([['b'], ['b'], ['b']])
  })
})
