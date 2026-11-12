/**
 * Unit tests for `ApiPath` and `API_BASE`: route roots start with a slash and are unique, and the
 * base is the prefix every request URL carries.
 */
import { describe, expect, it } from 'vitest'
import { API_BASE, ApiPath, reportApiPath } from '@/lib/apiPaths'

describe('ApiPath', () => {
  it('holds root-relative paths without a trailing slash', () => {
    for (const path of Object.values(ApiPath)) {
      expect(path.startsWith('/')).toBe(true)
      expect(path.endsWith('/')).toBe(false)
    }
  })

  it('holds no path twice', () => {
    const paths = Object.values(ApiPath)

    expect(new Set(paths).size).toBe(paths.length)
  })

  it('names the storefront content and e-mail roots under /admin', () => {
    expect([ApiPath.AdminFaqs, ApiPath.AdminLocations, ApiPath.AdminPartners, ApiPath.AdminProjects, ApiPath.AdminTestimonials, ApiPath.AdminSlides]).toEqual([
      '/admin/faqs',
      '/admin/locations',
      '/admin/partners',
      '/admin/projects',
      '/admin/testimonials',
      '/admin/slides',
    ])
    expect([ApiPath.AdminEmailLogs, ApiPath.AdminEmailTemplates]).toEqual(['/admin/email-logs', '/admin/email-templates'])
  })

  it('names the fraud detection roots under /admin/fraud', () => {
    expect([ApiPath.AdminFraudBlockList, ApiPath.AdminFraudLog, ApiPath.AdminFraudSettings]).toEqual([
      '/admin/fraud/blocklist',
      '/admin/fraud/log',
      '/admin/fraud/settings',
    ])
  })

  it('keeps the base the proxy mounts the API under', () => {
    expect(API_BASE).toBe('/api')
    expect(ApiPath.Auth).toBe('/auth')
  })
})

describe('reportApiPath', () => {
  it('builds report endpoints under the reports root, not the router page', () => {
    expect(reportApiPath('invoices')).toBe('/reports/invoices')
    expect(reportApiPath('invoices', 'export')).toBe('/reports/invoices/export')
  })
})
