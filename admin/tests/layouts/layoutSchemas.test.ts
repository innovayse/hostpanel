/**
 * Tests for the skeleton schemas of the three layouts with an inner navigation: each lists what the real layout
 * navigates with, under captions that resolve, so the skeleton shown while the layout downloads is the real one.
 */
import { describe, expect, it } from 'vitest'
import clientSchema, { ClientStatKey } from '@/layouts/ClientLayout.schema'
import integrationsSchema from '@/layouts/IntegrationsLayout.schema'
import reportsSchema from '@/layouts/ReportsLayout.schema'
import { CLIENT_NAV } from '@/lib/clientSections'
import { REPORTS } from '@/lib/reports'
import { translate } from '@/lib/translate'
import { INTEGRATION_CATEGORIES } from '@/types/integrationmeta'
import { SkeletonKind, SkeletonTabsOrientation } from '@/utils/ui/skeletonSchema'
import type { TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'

/**
 * Asserts that every caption key of a layout schema resolves in the English catalogue.
 *
 * @param schema - The schema to read.
 */
const expectCaptionsToResolve = (schema: TabsSkeletonSchema): void => {
  const keys = [...schema.tabs.map(tab => tab.label), ...(schema.stats ?? []).map(stat => stat.label)]

  expect(keys.length).toBeGreaterThan(0)
  for (const key of keys) {
    expect(translate(key)).not.toBe(key)
  }
}

describe('ClientLayout.schema', () => {
  it('is a tab layout with the section tabs of the client strip, in order, under a horizontal strip', () => {
    expect(clientSchema.kind).toBe(SkeletonKind.Tabs)
    expect(clientSchema.orientation).toBeUndefined()
    expect(clientSchema.tabs).toEqual(CLIENT_NAV.map(entry => ({ key: entry.key, label: entry.label })))
    expectCaptionsToResolve(clientSchema)
  })

  it('lists the five key figures of the header card under the keys the layout fills', () => {
    expect((clientSchema.stats ?? []).map(stat => stat.key)).toEqual(Object.values(ClientStatKey))
  })
})

describe('ReportsLayout.schema', () => {
  it('is a vertical rail with one entry per report, named by its menu caption', () => {
    expect(reportsSchema.kind).toBe(SkeletonKind.Tabs)
    expect(reportsSchema.orientation).toBe(SkeletonTabsOrientation.Vertical)
    expect(reportsSchema.tabs).toEqual(REPORTS.map(report => ({ key: report.slug, label: report.menuKey })))
    expectCaptionsToResolve(reportsSchema)
  })
})

describe('IntegrationsLayout.schema', () => {
  it('is a vertical rail with one entry per integration category', () => {
    expect(integrationsSchema.kind).toBe(SkeletonKind.Tabs)
    expect(integrationsSchema.orientation).toBe(SkeletonTabsOrientation.Vertical)
    expect(integrationsSchema.tabs).toEqual(INTEGRATION_CATEGORIES.map(category => ({ key: category.key, label: category.menuLabelKey })))
    expect(integrationsSchema.stripBelowMd).toBe(true)
    expectCaptionsToResolve(integrationsSchema)
  })
})
