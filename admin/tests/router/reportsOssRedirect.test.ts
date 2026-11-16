/**
 * The OSS report was first named after the scheme it replaced. A saved link or bookmark to the old
 * address `/reports/vat-moss` must still land on the report; a string redirect keeps the query, so the
 * quarter and year of the old link survive.
 */
import { describe, expect, it } from 'vitest'
import router from '@/router'
import { AppRoute } from '@/lib/routes'
import { REPORTS } from '@/lib/reports'

describe('old OSS report address', () => {
  it('redirects /reports/vat-moss to /reports/oss', () => {
    const resolved = router.resolve('/reports/vat-moss?year=2024&quarter=1')
    const leaf = resolved.matched[resolved.matched.length - 1]

    expect(leaf?.redirect).toBe(AppRoute.ReportsOss)
    expect(resolved.query).toEqual({ year: '2024', quarter: '1' })
  })

  it('lists the report under the new address and no report under the old one', () => {
    const slugs = REPORTS.map(report => report.slug)

    expect(slugs).toContain('oss')
    expect(slugs).not.toContain('vat-moss')
  })
})
