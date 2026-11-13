/**
 * Tests for the shared icon module: every named icon and every report icon parses into at least
 * one SVG shape node through the same whitelist `UiIcon` uses, and the report lookup falls back.
 */
import { describe, expect, it } from 'vitest'
import { FALLBACK_REPORT_ICON, Icon, REPORT_ICONS, reportIcon } from '@/lib/icons'
import { svgNodes } from '@/utils/ui/svgIcon'

describe('Icon', () => {
  it.each(Object.entries(Icon))('%s parses into shapes', (_name, markup) => {
    expect(svgNodes(markup).length).toBeGreaterThan(0)
  })

  it('keeps every markup distinct, so two names never draw the same thing', () => {
    const markups = Object.values(Icon)

    expect(new Set(markups).size).toBe(markups.length)
  })
})

describe('Icon.Users', () => {
  it('draws two people from stroked paths and circles only, with no stray glyph', () => {
    const nodes = svgNodes(Icon.Users)

    expect(nodes.map((node) => node.type)).toEqual(['path', 'circle', 'path', 'path'])
  })

  it('is the icon the users report uses', () => {
    expect(REPORT_ICONS['users']).toBe(Icon.Users)
  })
})

describe('REPORT_ICONS', () => {
  it.each(Object.entries(REPORT_ICONS))('%s parses into shapes', (_name, markup) => {
    expect(svgNodes(markup).length).toBeGreaterThan(0)
  })
})

describe('reportIcon', () => {
  it('returns the markup of a known icon', () => {
    expect(reportIcon('clock')).toBe(REPORT_ICONS['clock'])
  })

  it('returns the fallback for an unknown name', () => {
    expect(reportIcon('nope')).toBe(FALLBACK_REPORT_ICON)
  })
})
