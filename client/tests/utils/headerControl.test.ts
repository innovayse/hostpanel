/**
 * Unit tests for the header control class strings: one shared frame, classic tokens only.
 */
import { describe, expect, it } from 'vitest'
import {
  HEADER_CONTROL, HEADER_FOCUS, HEADER_ICON_ROW, HEADER_MENU_ROW, HEADER_NAV_ACTIVE, HEADER_NAV_IDLE, HEADER_NAV_ITEM,
} from '~/utils/headerControl'

describe('header control classes', () => {
  it('gives every control the same height, radius and border', () => {
    expect(HEADER_CONTROL).toContain('h-10')
    expect(HEADER_CONTROL).toContain('rounded-lg')
    expect(HEADER_CONTROL).toContain('border-classic-border')
    expect(HEADER_NAV_ITEM).toContain('h-10')
  })

  it('draws a keyboard focus ring in light and dark', () => {
    expect(HEADER_FOCUS).toContain('focus-visible:ring-classic-brand')
    expect(HEADER_FOCUS).toContain('dark:focus-visible:ring-classic-brand')
    for (const classes of [HEADER_CONTROL, HEADER_NAV_ITEM, HEADER_MENU_ROW, HEADER_ICON_ROW]) {
      expect(classes).toContain(HEADER_FOCUS)
    }
  })

  it('paints hover and active states from classic tokens', () => {
    expect(HEADER_CONTROL).toContain('hover:border-classic-brand')
    expect(HEADER_NAV_IDLE).toContain('hover:bg-classic-surface-2')
    expect(HEADER_NAV_ACTIVE).toContain('bg-classic-fill')
    expect(HEADER_NAV_ACTIVE).toContain('text-classic-on-brand')
  })
})
