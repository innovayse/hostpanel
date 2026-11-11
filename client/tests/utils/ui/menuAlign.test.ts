/**
 * Unit tests for `utils/ui/menuAlign.ts`: the edge classes per alignment, the viewport cap, and
 * the flip to the other edge when only that one keeps a menu on screen.
 *
 * @module tests/utils/ui/menuAlign.test
 */

import { describe, expect, it } from 'vitest'
import { UiMenuAlign } from '~/utils/ui/enums'
import { MENU_VIEWPORT_CAP, menuAlignClasses, resolveMenuAlign } from '~/utils/ui/menuAlign'

describe('menuAlignClasses', () => {
  it('anchors an end-aligned menu to the trigger\'s right edge', () => {
    expect(menuAlignClasses(UiMenuAlign.End).split(' ')).toEqual(['absolute', 'right-0', MENU_VIEWPORT_CAP])
  })

  it('anchors a start-aligned menu to the trigger\'s left edge', () => {
    expect(menuAlignClasses(UiMenuAlign.Start).split(' ')).toEqual(['absolute', 'left-0', MENU_VIEWPORT_CAP])
  })
})

describe('resolveMenuAlign', () => {
  it('keeps the end edge for an avatar at the right end of a wide header', () => {
    expect(resolveMenuAlign(UiMenuAlign.End, 1400, 1440, 280, 1920)).toBe(UiMenuAlign.End)
  })

  it('flips to the start edge when the end edge would run past the left of the screen', () => {
    expect(resolveMenuAlign(UiMenuAlign.End, 16, 56, 280, 390)).toBe(UiMenuAlign.Start)
  })

  it('flips to the end edge when the start edge would run past the right of the screen', () => {
    expect(resolveMenuAlign(UiMenuAlign.Start, 1400, 1440, 280, 1500)).toBe(UiMenuAlign.End)
  })

  it('keeps the preferred edge when neither edge fits', () => {
    expect(resolveMenuAlign(UiMenuAlign.End, 150, 190, 380, 390)).toBe(UiMenuAlign.End)
    expect(resolveMenuAlign(UiMenuAlign.Start, 150, 190, 380, 390)).toBe(UiMenuAlign.Start)
  })
})
