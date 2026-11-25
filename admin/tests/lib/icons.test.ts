/**
 * Tests for the shared icon module: every named icon and every report icon parses into at least
 * one SVG shape node through the same whitelist `UiIcon` uses, and the report lookup falls back.
 */
import { describe, expect, it } from 'vitest'
import { DEFAULT_PLUGIN_ICON, FALLBACK_REPORT_ICON, Icon, pluginIcon, REPORT_ICONS, reportIcon } from '@/lib/icons'
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

describe('Icon.Printer', () => {
  it('draws the paper tray, the body and the output sheet', () => {
    expect(svgNodes(Icon.Printer).map((node) => node.type)).toEqual(['polyline', 'path', 'rect'])
  })
})

describe('Icon.MoreHorizontal', () => {
  it('is three dots in a row, the "…" trigger of the row actions menu', () => {
    expect(svgNodes(Icon.MoreHorizontal).map((node) => node.type)).toEqual(['circle', 'circle', 'circle'])
  })
})

describe('Icon.SignOut, Icon.Key and Icon.Terminal', () => {
  it('draw the door with its arrow, the key, and the prompt over its cursor line', () => {
    expect(svgNodes(Icon.SignOut).map((node) => node.type)).toEqual(['path', 'polyline', 'line'])
    expect(svgNodes(Icon.Key).map((node) => node.type)).toEqual(['path'])
    expect(svgNodes(Icon.Terminal).map((node) => node.type)).toEqual(['polyline', 'line'])
  })
})

describe('Icon.Undo', () => {
  it('is an arrowhead and a counter-clockwise arc', () => {
    expect(svgNodes(Icon.Undo).map((node) => node.type)).toEqual(['polyline', 'path'])
  })
})

describe('Icon.ShieldCheck and Icon.ShieldOff', () => {
  it('draw the shield with a tick and the shield struck through', () => {
    expect(svgNodes(Icon.ShieldCheck).map((node) => node.type)).toEqual(['path', 'polyline'])
    expect(svgNodes(Icon.ShieldOff).map((node) => node.type)).toEqual(['path', 'path', 'line'])
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

describe('Icon.Play, Icon.Send and Icon.ArrowRight', () => {
  it('draw the triangle, the paper plane and the right arrow', () => {
    expect(svgNodes(Icon.Play).map((node) => node.type)).toEqual(['polygon'])
    expect(svgNodes(Icon.Send).map((node) => node.type)).toEqual(['line', 'polygon'])
    expect(svgNodes(Icon.ArrowRight).map((node) => node.type)).toEqual(['line', 'polyline'])
  })
})

describe('Icon.User and Icon.Activity', () => {
  it('draw a person and a heartbeat line, the icons of the identity and background job integration categories', () => {
    expect(svgNodes(Icon.User).map((node) => node.type)).toEqual(['path', 'circle'])
    expect(svgNodes(Icon.Activity).map((node) => node.type)).toEqual(['polyline'])
  })
})

describe('pluginIcon', () => {
  it('maps an icon name to a built-in icon and anything else to the default', () => {
    expect(pluginIcon('chart')).not.toBe(DEFAULT_PLUGIN_ICON)
    expect(pluginIcon('https://evil.test/x.svg')).toBe(DEFAULT_PLUGIN_ICON)
    expect(pluginIcon('__proto__')).toBe(DEFAULT_PLUGIN_ICON)
    expect(pluginIcon(null)).toBe(DEFAULT_PLUGIN_ICON)
  })

  it('draws every named icon and the default as shapes', () => {
    expect(svgNodes(DEFAULT_PLUGIN_ICON).length).toBeGreaterThan(0)
    expect(svgNodes(pluginIcon('rocket')).length).toBeGreaterThan(0)
  })
})
