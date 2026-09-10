/**
 * Tests for the colour-mode resolution rule: the visitor's cookie beats the operator's
 * default, the operator's default beats the device, and anything unrecognised falls back
 * to the mode the storefront always had.
 *
 * @module utils/colorMode.test
 */

import { describe, expect, it } from 'vitest'
import {
  DEFAULT_COLOR_MODE_SETTING,
  isUserToggleEnabled,
  parseColorMode,
  parseColorModeSetting,
  resolveColorMode,
} from './colorMode'

describe('parseColorModeSetting', () => {
  it('accepts the three operator values', () => {
    expect(parseColorModeSetting('light')).toBe('light')
    expect(parseColorModeSetting('dark')).toBe('dark')
    expect(parseColorModeSetting('system')).toBe('system')
  })

  it('degrades anything else to the shipped default', () => {
    expect(parseColorModeSetting('')).toBe(DEFAULT_COLOR_MODE_SETTING)
    expect(parseColorModeSetting(undefined)).toBe(DEFAULT_COLOR_MODE_SETTING)
    expect(parseColorModeSetting('Light')).toBe(DEFAULT_COLOR_MODE_SETTING)
    expect(parseColorModeSetting('auto')).toBe(DEFAULT_COLOR_MODE_SETTING)
  })
})

describe('parseColorMode', () => {
  it('never returns system — a page cannot be in that mode', () => {
    expect(parseColorMode('system')).toBeNull()
    expect(parseColorMode('light')).toBe('light')
  })
})

describe('resolveColorMode', () => {
  it('lets the cookie win over the operator default', () => {
    expect(resolveColorMode('light', 'dark')).toBe('light')
    expect(resolveColorMode('dark', 'light')).toBe('dark')
    expect(resolveColorMode('light', 'system')).toBe('light')
  })

  it('uses the operator default when there is no cookie', () => {
    expect(resolveColorMode(undefined, 'light')).toBe('light')
    expect(resolveColorMode(null, 'dark')).toBe('dark')
  })

  it('defers to the device only when nothing else decides', () => {
    expect(resolveColorMode(undefined, 'system')).toBeNull()
  })

  it('ignores a cookie it does not understand', () => {
    expect(resolveColorMode('purple', 'light')).toBe('light')
    expect(resolveColorMode('purple', 'system')).toBeNull()
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
