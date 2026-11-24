/**
 * Unit tests for the link type sizes (navigation 14px, sub-menu 13px) and the classes of a navigation entry.
 */
import { describe, expect, it } from 'vitest'
import { navGuideClasses, navIconClasses, navItemClasses, navItemTextClasses, navListClasses, navSubItemTextClasses, UiNavItemLevel } from '@/utils/ui/nav'

describe('nav type sizes', () => {
  it('sets a navigation entry at 14px', () => {
    expect(navItemTextClasses.split(' ')).toContain('text-sm')
  })

  it('sets a sub-menu entry at 13px, smaller than a navigation entry', () => {
    expect(navSubItemTextClasses.split(' ')).toContain('text-[0.8125rem]')
    expect(navSubItemTextClasses).not.toContain('text-sm')
  })
})

describe('navigation entry classes', () => {
  it('shapes a top-level entry at 42px, folded to its centred icon in the rail', () => {
    expect(navItemClasses(UiNavItemLevel.Top, false, false).join(' ')).toContain('h-[2.625rem]')
    expect(navItemClasses(UiNavItemLevel.Top, false, false)).toContain('gap-3 px-3')
    expect(navItemClasses(UiNavItemLevel.Top, false, true)).toContain('justify-center px-0')
  })

  it('marks the current page of either level and colours its icon with the accent', () => {
    expect(navItemClasses(UiNavItemLevel.Top, true, false).join(' ')).toContain('bg-(--nav-active-bg)')
    expect(navItemClasses(UiNavItemLevel.Sub, true, false).join(' ')).toContain('text-text-accent')
    expect(navItemClasses(UiNavItemLevel.Sub, false, false).join(' ')).toContain('text-text-muted')
    expect(navIconClasses(true)).toBe('text-text-accent')
    expect(navIconClasses(false)).toBe('text-text-muted')
  })

  it('lights the guide down to the active link only', () => {
    expect(navGuideClasses(0, -1)).toBe('')
    expect(navGuideClasses(3, 1)).toBe('')
    expect(navGuideClasses(0, 1)).toBe('nav-guide-lit [--i:0]')
    expect(navGuideClasses(1, 1)).toBe('nav-guide-lit [--i:1] nav-guide-active')
  })

  it('measures the lit path of a sub-menu list from the active link', () => {
    expect(navListClasses(-1)).toBe('py-(--nav-guide-gap) pl-9 gap-(--nav-guide-gap)')
    expect(navListClasses(2)).toBe('py-(--nav-guide-gap) pl-9 gap-(--nav-guide-gap) [--n:2]')
  })
})
