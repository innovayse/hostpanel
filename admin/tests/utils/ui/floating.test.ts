/**
 * Tests for the floating panel placement rules: which edge the panel hangs from, when it opens above and how the
 * place reaches the element as CSS variables.
 */
import { describe, expect, it } from 'vitest'
import { FLOATING_PANEL_CLASSES, FloatingAlign, applyFloatingPlace, computeFloatingPlace } from '@/utils/ui/floating'

/** A trigger 200px wide near the top left of a 1000x800 viewport. */
const anchor = { left: 100, right: 300, top: 100, bottom: 140, width: 200 }

/** The viewport the anchor sits in. */
const viewport = { width: 1000, height: 800 }

describe('computeFloatingPlace', () => {
  it('hangs a start aligned panel from the left edge, below the trigger, as wide as the trigger', () => {
    expect(computeFloatingPlace(anchor, viewport, { align: FloatingAlign.Start, minRoom: 160, gap: 6 }))
      .toEqual({ left: 100, top: 146, width: 200 })
  })

  it('hangs an end aligned panel from the right edge', () => {
    expect(computeFloatingPlace(anchor, viewport, { align: FloatingAlign.End, minRoom: 160, gap: 4 }))
      .toEqual({ right: 700, top: 144, width: 200 })
  })

  it('opens above the trigger when there is little room below and more above', () => {
    const low = { left: 100, right: 300, top: 700, bottom: 740, width: 200 }

    expect(computeFloatingPlace(low, viewport, { align: FloatingAlign.Start, minRoom: 160, gap: 4 }))
      .toEqual({ left: 100, bottom: 104, width: 200 })
  })

  it('stays below when there is little room on both sides, the larger one wins', () => {
    const tight = { left: 0, right: 100, top: 10, bottom: 50, width: 100 }

    expect(computeFloatingPlace(tight, { width: 1000, height: 200 }, { align: FloatingAlign.Start, minRoom: 300, gap: 4 }).top).toBe(54)
  })

  it('pulls a wide panel back inside the viewport, but never past the left margin', () => {
    const near = { left: 800, right: 900, top: 0, bottom: 40, width: 100 }

    expect(computeFloatingPlace(near, viewport, { align: FloatingAlign.Start, minRoom: 0, gap: 8, keepInsideWidth: 320 }).left).toBe(664)
    expect(computeFloatingPlace(near, { width: 300, height: 800 }, { align: FloatingAlign.Start, minRoom: 0, gap: 8, keepInsideWidth: 900 }).left).toBe(8)
  })
})

describe('applyFloatingPlace', () => {
  it('writes the used edges as CSS variables and removes the unused ones', () => {
    const element = document.createElement('div')
    element.style.setProperty('--fp-bottom', '9px')

    applyFloatingPlace(element, { left: 10, top: 20, width: 30 })

    expect(element.style.getPropertyValue('--fp-left')).toBe('10px')
    expect(element.style.getPropertyValue('--fp-top')).toBe('20px')
    expect(element.style.getPropertyValue('--fp-width')).toBe('30px')
    expect(element.style.getPropertyValue('--fp-bottom')).toBe('')
    expect(element.style.getPropertyValue('--fp-right')).toBe('')
  })
})

describe('FLOATING_PANEL_CLASSES', () => {
  it('is an opaque raised surface with a border and the popover shadow, and no glass or translucent utility', () => {
    const classes = FLOATING_PANEL_CLASSES.split(' ')

    expect(classes).toEqual(expect.arrayContaining(['bg-surface-elevated', 'border', 'border-border', 'shadow-popover']))
    expect(FLOATING_PANEL_CLASSES).not.toMatch(/glass|backdrop-blur|bg-surface-card|\/\d/)
  })
})
