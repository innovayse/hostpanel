import { describe, expect, it } from 'vitest'
import { DEFAULT_TEMPLATE, TEMPLATE_NAMES, resolveTemplateName } from './types'

describe('resolveTemplateName', () => {
  it('accepts every known template name', () => {
    for (const name of TEMPLATE_NAMES) {
      expect(resolveTemplateName(name)).toBe(name)
    }
  })

  it('falls back to the default when the value is missing', () => {
    expect(resolveTemplateName(undefined)).toBe(DEFAULT_TEMPLATE)
    expect(resolveTemplateName(null)).toBe(DEFAULT_TEMPLATE)
    expect(resolveTemplateName('')).toBe(DEFAULT_TEMPLATE)
  })

  it('falls back to the default when the value is unknown', () => {
    expect(resolveTemplateName('nope')).toBe(DEFAULT_TEMPLATE)
    expect(resolveTemplateName('  aurora  ')).toBe(DEFAULT_TEMPLATE)
  })

  it('is case-sensitive, so a miscased value falls back rather than half-matching', () => {
    expect(resolveTemplateName('Aurora')).toBe(DEFAULT_TEMPLATE)
    expect(resolveTemplateName('CLASSIC')).toBe(DEFAULT_TEMPLATE)
  })

  it('defaults to aurora', () => {
    expect(DEFAULT_TEMPLATE).toBe('aurora')
  })
})
