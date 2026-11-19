/**
 * Tests for the report registry the sidebar and router share, and for the fill scale of the reports world map.
 */
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { filterReports, MAP_FILL_STEPS, MAP_NO_DATA_FILL, mapFillClass, mapFillIndex, REPORTS, reportGroupLabel, ReportGroup } from '@/lib/reports'
import { reportIcon } from '@/lib/icons'
import { translate } from '@/lib/translate'

/** The slugs of each group in the order the menu lists them (importance first). */
const EXPECTED_ORDER: Record<ReportGroup, string[]> = {
  [ReportGroup.Overview]: ['daily-performance', 'annual-income', 'income-forecast', 'income-by-product'],
  [ReportGroup.Finance]: ['invoices', 'aging-invoices', 'monthly-transactions', 'transactions', 'sales-tax', 'oss', 'direct-debit', 'credits-reviewer'],
  [ReportGroup.Clients]: ['clients', 'new-customers', 'top-clients', 'clients-by-country', 'customer-retention', 'client-statement'],
  [ReportGroup.OrdersServices]: ['monthly-orders', 'services', 'product-suspensions', 'promotions'],
  [ReportGroup.Domains]: ['domains', 'domain-renewal-emails', 'ssl-monitoring'],
  [ReportGroup.Support]: ['support-ticket-replies', 'ticket-tags', 'ticket-feedback-scores', 'ticket-feedback-comments', 'ticket-ratings-reviewer'],
  [ReportGroup.Servers]: ['disk-usage', 'server-revenue-forecasts'],
}

describe('REPORTS', () => {
  it('has unique slugs, a full title, a short menu caption and an icon for every report', () => {
    const slugs = REPORTS.map(report => report.slug)

    expect(new Set(slugs).size).toBe(slugs.length)
    expect(REPORTS).toHaveLength(32)
    for (const report of REPORTS) {
      expect(report.label).not.toBe('')
      expect(report.menuLabel).not.toBe('')
      expect(report.menuLabel).not.toMatch(/^reports\./)
      expect(reportIcon(report.icon)).not.toBe('')
    }
  })

  it('puts every report in a known group and loses none', () => {
    const groups = Object.values(ReportGroup)

    for (const report of REPORTS) {
      expect(groups).toContain(report.group)
    }
    const listed = Object.values(EXPECTED_ORDER).flat()
    expect(listed).toHaveLength(REPORTS.length)
    expect(REPORTS.map(report => report.slug).sort()).toEqual([...listed].sort())
  })

  it('lists the groups in order and the reports of a group by importance', () => {
    expect(REPORTS.map(report => report.slug)).toEqual(Object.values(EXPECTED_ORDER).flat())
    for (const [group, slugs] of Object.entries(EXPECTED_ORDER)) {
      expect(REPORTS.filter(report => report.group === group).map(report => report.slug)).toEqual(slugs)
    }
  })

  it('has a heading for every group', () => {
    for (const group of Object.values(ReportGroup)) {
      expect(reportGroupLabel(group)).not.toMatch(/^reports\./)
    }
  })

  it('names the view file of every report, which exists, and the locale key of its menu caption', () => {
    for (const report of REPORTS) {
      expect(existsSync(join(__dirname, '..', '..', 'src', report.file))).toBe(true)
      expect(report.file).toMatch(/^views\/[A-Za-z0-9]+View\.vue$/)
      expect(translate(report.menuKey)).toBe(report.menuLabel)
    }
    expect(REPORTS[0]?.slug).toBe('daily-performance')
    expect(REPORTS[0]?.file).toBe('views/DailyPerformanceView.vue')
  })
})

describe('filterReports', () => {
  it('keeps everything for a blank query', () => {
    expect(filterReports(REPORTS, '  ')).toHaveLength(REPORTS.length)
  })

  it('matches the caption, the full title or the slug, ignoring case', () => {
    expect(filterReports(REPORTS, 'OVERDUE').map(report => report.slug)).toContain('aging-invoices')
    expect(filterReports(REPORTS, 'suspensions').map(report => report.slug)).toEqual(['product-suspensions'])
  })

  it('returns nothing when nothing matches', () => {
    expect(filterReports(REPORTS, 'zzzz-no-such-report')).toEqual([])
  })
})

describe('mapFillIndex', () => {
  it('gives the lightest step to a small value and the darkest to the maximum', () => {
    expect(mapFillIndex(1, 100)).toBe(0)
    expect(mapFillIndex(100, 100)).toBe(MAP_FILL_STEPS.length - 1)
  })

  it('rises with the value', () => {
    const indexes = [10, 30, 50, 70, 100].map(value => mapFillIndex(value, 100))

    expect(indexes).toEqual([0, 1, 2, 3, 4])
  })

  it('has no step for zero, negative values or an empty scale', () => {
    expect(mapFillIndex(0, 10)).toBe(-1)
    expect(mapFillIndex(-3, 10)).toBe(-1)
    expect(mapFillIndex(5, 0)).toBe(-1)
  })

  it('never goes past the darkest step', () => {
    expect(mapFillIndex(500, 100)).toBe(MAP_FILL_STEPS.length - 1)
  })
})

describe('mapFillClass', () => {
  it('is neutral for a country without data or without clients', () => {
    expect(mapFillClass(undefined, 10)).toBe(MAP_NO_DATA_FILL)
    expect(mapFillClass(0, 10)).toBe(MAP_NO_DATA_FILL)
  })

  it('is the step class for a country with data', () => {
    expect(mapFillClass(10, 10)).toBe(MAP_FILL_STEPS.at(-1)?.fill)
    expect(mapFillClass(1, 10)).toBe(MAP_FILL_STEPS[0]?.fill)
  })
})
