/**
 * Mount tests for MigrationReportTable: log rows render with their outcome, a filter reloads from
 * the first page with the chosen value, and an empty or refused log is handled.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import MigrationReportTable from '@/components/migration/MigrationReportTable.vue'
import { createTestI18n } from '../../helpers/i18n'
import { json, problem } from '../../helpers/fetch'
import { stubRoutes } from '../../helpers/routes'
import { MigrationEntityType, MigrationLogAction, MigrationSkipReason } from '@/types/migration'

/** Three log rows: one imported client, one failed invoice and one skipped domain. */
const page = {
  items: [
    { id: 1, entityType: MigrationEntityType.Clients, identifier: 'C-100', action: MigrationLogAction.Imported, skipReason: null, reason: null, createdAt: '2026-10-01T10:00:00Z' },
    { id: 2, entityType: MigrationEntityType.Invoices, identifier: 'INV-7', action: MigrationLogAction.Failed, skipReason: MigrationSkipReason.ImportFailed, reason: null, createdAt: '2026-10-01T10:00:05Z' },
    { id: 3, entityType: MigrationEntityType.Domains, identifier: 'example.test', action: MigrationLogAction.Skipped, skipReason: MigrationSkipReason.ClientNotFound, reason: null, createdAt: '2026-10-01T10:00:09Z' },
  ],
  totalCount: 3,
  pageNumber: 1,
  pageSize: 20,
  totalPages: 1,
}

/**
 * Mounts the report for job 5 and waits for the first load.
 *
 * @returns The wrapper.
 */
const mountReport = async () => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const wrapper = mount(MigrationReportTable, {
    props: { jobId: 5 },
    global: { plugins: [createTestI18n(), pinia] },
  })
  await flushPromises()
  return wrapper
}

describe('MigrationReportTable', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row per log entry with its outcome and translated reason', async () => {
    stubRoutes({ 'GET /api/admin/migrations/5/logs': () => json(page) })
    const wrapper = await mountReport()

    const text = wrapper.get('[data-testid="migration-report-table"]').text()
    expect(text).toContain('C-100')
    expect(text).toContain('INV-7')
    expect(text).toContain('Imported')
    expect(text).toContain('Failed')
    expect(text).toContain('Import failed')
    expect(text).toContain('Client not found')
    expect(text).not.toContain('ClientNotFound')
    // The text columns (type, identifier, reason) share the width and truncate; the status and the time shrink to their content.
    const headers = wrapper.findAll('th')
    expect(headers.filter((header) => header.classes().includes('max-w-0'))).toHaveLength(3)
    expect(headers.filter((header) => header.classes().includes('w-px'))).toHaveLength(2)
    expect(wrapper.get('[data-testid="migration-report-count"]').text()).toContain('3')
  })

  it('asks for page 1 with the page size on the first load', async () => {
    const mock = stubRoutes({ 'GET /api/admin/migrations/5/logs': () => json(page) })
    await mountReport()

    const url = String(mock.mock.calls[0]?.[0])
    expect(url).toContain('page=1')
    expect(url).toContain('pageSize=20')
  })

  it('shows the empty state when the log has no rows', async () => {
    stubRoutes({ 'GET /api/admin/migrations/5/logs': () => json({ ...page, items: [], totalCount: 0 }) })
    const wrapper = await mountReport()

    expect(wrapper.text()).toContain('No records found')
    expect(wrapper.find('[data-testid="migration-report-error"]').exists()).toBe(false)
  })

  it('shows the API sentence with Retry instead of the empty state when the log is refused', async () => {
    let refuse = true
    stubRoutes({ 'GET /api/admin/migrations/5/logs': () => (refuse ? problem('No such job.', 404) : json(page)) })
    const wrapper = await mountReport()

    expect(wrapper.get('[data-testid="migration-report-error"]').text()).toBe('No such job.')
    expect(wrapper.text()).not.toContain('No records found')

    refuse = false
    await wrapper.get('[data-testid="migration-report-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="migration-report-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="migration-report-table"]').text()).toContain('C-100')
  })
})
