/**
 * Tests for the logo choice: dark mode prefers the dark logo, everything falls back to
 * the light one, and the mark falls back to the wordmark.
 *
 * @module utils/brandLogo.test
 */

import { describe, expect, it } from 'vitest'
import { pickBrandLogo, pickBrandMark, resolveBrandLogos } from './brandLogo'

const all = { light: '/l.png', dark: '/d.png', mark: '/m.png' }
const lightOnly = { light: '/l.png', dark: '', mark: '' }
const none = { light: '', dark: '', mark: '' }

describe('pickBrandLogo', () => {
  it('uses the dark logo in dark mode when there is one', () => {
    expect(pickBrandLogo(all, true)).toBe('/d.png')
  })

  it('falls back to the light logo in dark mode — the pre-existing behaviour', () => {
    expect(pickBrandLogo(lightOnly, true)).toBe('/l.png')
  })

  it('always uses the light logo in light mode', () => {
    expect(pickBrandLogo(all, false)).toBe('/l.png')
  })

  it('is empty when nothing was uploaded, so the built-in mark renders', () => {
    expect(pickBrandLogo(none, true)).toBe('')
    expect(pickBrandLogo(none, false)).toBe('')
  })
})

describe('pickBrandMark', () => {
  it('prefers the uploaded mark in either mode', () => {
    expect(pickBrandMark(all, true)).toBe('/m.png')
    expect(pickBrandMark(all, false)).toBe('/m.png')
  })

  it('falls back to the wordmark for the mode', () => {
    expect(pickBrandMark({ ...all, mark: '' }, true)).toBe('/d.png')
    expect(pickBrandMark(lightOnly, false)).toBe('/l.png')
  })

  it('is empty when nothing was uploaded', () => {
    expect(pickBrandMark(none, true)).toBe('')
  })
})

describe('resolveBrandLogos', () => {
  it('returns both wordmarks and flags a real dark variant', () => {
    expect(resolveBrandLogos(all)).toEqual({ light: '/l.png', dark: '/d.png', hasDarkVariant: true })
  })

  it('falls the dark slot back to the light logo and says so', () => {
    expect(resolveBrandLogos(lightOnly)).toEqual({ light: '/l.png', dark: '/l.png', hasDarkVariant: false })
  })

  it('is empty on both sides when nothing was uploaded', () => {
    expect(resolveBrandLogos(none)).toEqual({ light: '', dark: '', hasDarkVariant: false })
  })

  it('serves a dark-only upload in both modes rather than nothing in light', () => {
    expect(resolveBrandLogos({ light: '', dark: '/d.png', mark: '' })).toEqual({ light: '/d.png', dark: '/d.png', hasDarkVariant: false })
  })
})
