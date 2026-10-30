/**
 * Unit tests for the product-type icon map: every type has a full, literal icon name and an unknown
 * type falls back to the default rather than to an empty tile.
 */
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { describe, expect, it } from 'vitest'
import { DEFAULT_PRODUCT_ICON, PRODUCT_TYPE_ICON, productTypeIcon } from '~/constants/productTypeIcon'
import { ProductType } from '~/types/producttype'

describe('PRODUCT_TYPE_ICON', () => {
  it('names an icon for every product type, always with its set prefix', () => {
    for (const type of Object.values(ProductType)) {
      expect(PRODUCT_TYPE_ICON[type]).toMatch(/^mdi:[a-z0-9-]+$/)
    }
  })

  it('gives every known type its own icon, none of them the default', () => {
    const known = Object.values(ProductType).filter((type) => type !== ProductType.Other)
    const icons = known.map((type) => PRODUCT_TYPE_ICON[type])

    expect(new Set(icons).size).toBe(known.length)
    expect(icons).not.toContain(DEFAULT_PRODUCT_ICON)
  })

  it('names only icons that exist in the installed set', () => {
    // Scan the raw file for the entry keys instead of importing the 3 MB set as a module: the
    // icons and aliases sit at a fixed indent, so a key lookup proves the name exists without a parse.
    const raw = readFileSync(createRequire(import.meta.url).resolve('@iconify-json/mdi/icons.json'), 'utf8')
    for (const name of [...Object.values(PRODUCT_TYPE_ICON), DEFAULT_PRODUCT_ICON]) {
      const short = name.replace(/^mdi:/, '')
      expect(raw.includes(`\n\t\t"${short}": {`)).toBe(true)
    }
  })
})

describe('productTypeIcon', () => {
  it('returns the icon of a known type', () => {
    expect(productTypeIcon(ProductType.Vps)).toBe('mdi:server-network')
    expect(productTypeIcon(ProductType.Dedicated)).toBe('mdi:server')
    expect(productTypeIcon(ProductType.Email)).toBe('mdi:email-outline')
    expect(productTypeIcon(ProductType.Addon)).toBe('mdi:puzzle-outline')
    expect(productTypeIcon(ProductType.Domain)).toBe('mdi:earth')
    expect(productTypeIcon(ProductType.Digital)).toBe('mdi:download-box')
  })

  it('falls back to the default for a missing or unknown type', () => {
    expect(productTypeIcon(null)).toBe(DEFAULT_PRODUCT_ICON)
    expect(productTypeIcon(undefined)).toBe(DEFAULT_PRODUCT_ICON)
    // A value a newer backend may send that this build's enum does not know yet.
    expect(productTypeIcon('Mystery' as ProductType)).toBe(DEFAULT_PRODUCT_ICON)
  })
})
