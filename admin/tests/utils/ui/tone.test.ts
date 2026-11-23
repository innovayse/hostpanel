/**
 * Unit tests for the tone vocabulary: every tone has a class in every map.
 */
import { describe, expect, it } from 'vitest'
import {
  toneAlertClasses,
  toneBadgeClasses,
  toneDotClasses,
  toneTextClasses,
  UiTone,
} from '@/utils/ui/tone'

describe('tone maps', () => {
  const tones = Object.values(UiTone)

  it.each([
    ['badge', toneBadgeClasses],
    ['alert', toneAlertClasses],
    ['text', toneTextClasses],
    ['dot', toneDotClasses],
  ])('the %s map covers every tone with non-empty classes', (_name, map) => {
    for (const tone of tones) {
      expect(map[tone].length).toBeGreaterThan(0)
    }
  })

  it('draws a coloured badge as a status tint and the status colour, without a border or a per-theme step', () => {
    for (const tone of tones.filter((t) => t !== UiTone.Neutral)) {
      expect(toneBadgeClasses[tone]).toMatch(/bg-tint-/)
      expect(toneBadgeClasses[tone]).toMatch(/text-(status|text-accent)/)
      expect(toneBadgeClasses[tone]).not.toMatch(/border/)
      expect(toneAlertClasses[tone]).toMatch(/border-/)
      expect(toneBadgeClasses[tone]).not.toContain('light:')
      expect(toneTextClasses[tone]).not.toContain('light:')
    }
  })

  it('exposes the named members as their literal values', () => {
    expect(UiTone.Danger).toBe('danger')
    expect(UiTone.Primary).toBe('primary')
  })
})
