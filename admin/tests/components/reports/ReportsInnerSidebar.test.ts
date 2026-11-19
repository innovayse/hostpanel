/**
 * Tests for ReportsInnerSidebar: one link per report, the open one marked.
 */
import { describe, expect, it } from 'vitest'
import { REPORTS, reportGroupLabel, ReportGroup } from '@/lib/reports'
import ReportsInnerSidebar from '@/components/reports/ReportsInnerSidebar.vue'
import { mountView } from '../../helpers/mount'

describe('ReportsInnerSidebar', () => {
  it('lists every report as a link to its slug', async () => {
    const wrapper = await mountView(ReportsInnerSidebar, { path: '/reports/clients' })
    const links = wrapper.findAll('a')

    expect(links).toHaveLength(REPORTS.length)
    expect(wrapper.get('[data-testid="reports-nav-domains"]').attributes('href')).toBe('/reports/domains')
  })

  it('marks only the open report as the current page', async () => {
    const wrapper = await mountView(ReportsInnerSidebar, { path: '/reports/clients' })

    expect(wrapper.get('[data-testid="reports-nav-clients"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-testid="reports-nav-domains"]').attributes('aria-current')).toBeUndefined()
  })

  it('marks nothing outside a report', async () => {
    const wrapper = await mountView(ReportsInnerSidebar, { path: '/dashboard' })

    expect(wrapper.find('[aria-current="page"]').exists()).toBe(false)
  })

  it('shows the report group headings and the short captions once the sidebar is open', async () => {
    const wrapper = await mountView(ReportsInnerSidebar, { path: '/reports/clients' })

    await wrapper.get('div').trigger('mouseenter')

    const headings = wrapper.findAll('p').map(heading => heading.text())
    expect(headings).toEqual(Object.values(ReportGroup).map(reportGroupLabel))
    const aging = wrapper.get('[data-testid="reports-nav-aging-invoices"]')
    expect(aging.text()).toBe(REPORTS.find(report => report.slug === 'aging-invoices')?.menuLabel)
    expect(aging.attributes('title')).toBe(REPORTS.find(report => report.slug === 'aging-invoices')?.label)
  })

  it('narrows the list with the search box and says so when nothing matches', async () => {
    const wrapper = await mountView(ReportsInnerSidebar, { path: '/reports/clients' })

    await wrapper.get('div').trigger('mouseenter')
    await wrapper.get('[data-testid="reports-nav-search"]').setValue('suspensions')

    expect(wrapper.findAll('a')).toHaveLength(1)
    expect(wrapper.find('[data-testid="reports-nav-product-suspensions"]').exists()).toBe(true)

    await wrapper.get('[data-testid="reports-nav-search"]').setValue('zzzz-nothing')

    expect(wrapper.findAll('a')).toHaveLength(0)
    expect(wrapper.find('[data-testid="reports-nav-empty"]').exists()).toBe(true)
  })

  it('lists every report in the folded rail whatever was typed before it folded', async () => {
    const wrapper = await mountView(ReportsInnerSidebar, { path: '/reports/clients' })

    await wrapper.get('div').trigger('mouseenter')
    await wrapper.get('[data-testid="reports-nav-search"]').setValue('suspensions')
    await wrapper.get('div').trigger('mouseleave')

    expect(wrapper.findAll('a')).toHaveLength(REPORTS.length)
  })
})
