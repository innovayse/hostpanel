/**
 * Unit tests for the link type sizes: navigation 14px, sub-menu 13px.
 */
import { describe, expect, it } from 'vitest'
import { navItemTextClasses, navSubItemTextClasses } from '@/utils/ui/nav'

describe('nav type sizes', () => {
  it('sets a navigation entry at 14px', () => {
    expect(navItemTextClasses.split(' ')).toContain('text-sm')
  })

  it('sets a sub-menu entry at 13px, smaller than a navigation entry', () => {
    expect(navSubItemTextClasses.split(' ')).toContain('text-[0.8125rem]')
    expect(navSubItemTextClasses).not.toContain('text-sm')
  })
})
