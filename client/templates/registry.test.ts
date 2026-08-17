import { describe, expect, it } from 'vitest'
import { TEMPLATE_NAMES } from './types'
import type { TemplateSlot } from './types'
import { templates } from './registry'

const SLOTS: TemplateSlot[] = ['header', 'footer', 'home', 'hosting', 'domains', 'checkout']

describe('template registry', () => {
  it('registers every known template', () => {
    expect(Object.keys(templates).sort()).toEqual([...TEMPLATE_NAMES].sort())
  })

  it('gives every template every slot, so no route can render nothing', () => {
    for (const name of TEMPLATE_NAMES) {
      expect(Object.keys(templates[name]).sort()).toEqual([...SLOTS].sort())
    }
  })

  it('registers loaders, not eagerly imported components', () => {
    for (const name of TEMPLATE_NAMES) {
      for (const slot of SLOTS) {
        expect(typeof templates[name][slot]).toBe('function')
      }
    }
  })
})
