/**
 * Tests for the colour-mode resolution rule: the visitor's cookie beats the operator's
 * default, the operator's default beats the device, and anything unrecognised falls back
 * to the mode the storefront always had.
 *
 * @module tests/utils/colorMode.test
 */

import { describe, expect, it } from 'vitest'
import {
  DEFAULT_COLOR_MODE_SETTING,
  isUserToggleEnabled,
  parseColorMode,
  parseColorModeSetting,
  resolveColorMode,
} from '~/utils/colorMode'
import { ColorMode } from '~/types/colormode'
import { ColorModeSystem } from '~/types/colormodesystem'

describe('parseColorModeSetting', () => {
  it('accepts the three operator values', () => {
    expect(parseColorModeSetting('light')).toBe(ColorMode.Light)
    expect(parseColorModeSetting(ColorMode.Dark)).toBe(ColorMode.Dark)
    expect(parseColorModeSetting(ColorModeSystem.System)).toBe(ColorModeSystem.System)
  })

  it('degrades anything else to the shipped default', () => {
    expect(parseColorModeSetting('')).toBe(DEFAULT_COLOR_MODE_SETTING)
    expect(parseColorModeSetting(undefined)).toBe(DEFAULT_COLOR_MODE_SETTING)
    expect(parseColorModeSetting('Light')).toBe(DEFAULT_COLOR_MODE_SETTING)
    expect(parseColorModeSetting('auto')).toBe(DEFAULT_COLOR_MODE_SETTING)
  })
})

describe('parseColorMode', () => {
  it('never returns system, a page cannot be in that mode', () => {
    expect(parseColorMode(ColorModeSystem.System)).toBeNull()
    expect(parseColorMode(ColorMode.Light)).toBe(ColorMode.Light)
  })
})

describe('resolveColorMode', () => {
  it('lets the cookie win over the operator default', () => {
    expect(resolveColorMode(ColorMode.Light, ColorMode.Dark)).toBe(ColorMode.Light)
    expect(resolveColorMode(ColorMode.Dark, ColorMode.Light)).toBe(ColorMode.Dark)
    expect(resolveColorMode(ColorMode.Light, ColorModeSystem.System)).toBe(ColorMode.Light)
  })

  it('uses the operator default when there is no cookie', () => {
    expect(resolveColorMode(undefined, ColorMode.Light)).toBe(ColorMode.Light)
    expect(resolveColorMode(null, ColorMode.Dark)).toBe(ColorMode.Dark)
  })

  it('defers to the device only when nothing else decides', () => {
    expect(resolveColorMode(undefined, ColorModeSystem.System)).toBeNull()
  })

  it('ignores a cookie it does not understand', () => {
    expect(resolveColorMode('purple', ColorMode.Light)).toBe(ColorMode.Light)
    expect(resolveColorMode('purple', ColorModeSystem.System)).toBeNull()
  })
})

describe('isUserToggleEnabled', () => {
  it('hides the switch only on the literal false', () => {
    expect(isUserToggleEnabled('false')).toBe(false)
    expect(isUserToggleEnabled('true')).toBe(true)
    expect(isUserToggleEnabled('')).toBe(true)
    expect(isUserToggleEnabled(undefined)).toBe(true)
  })
})
