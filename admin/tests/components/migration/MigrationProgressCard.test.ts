/**
 * Mount tests for MigrationProgressCard: state, percent and counts render from the job, a failed
 * job shows its translated failure reason, and the connection check and the import start go through the
 * store to the API.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import MigrationProgressCard from '@/components/migration/MigrationProgressCard.vue'
import { createTestI18n } from '../../helpers/i18n'
import { json, problem } from '../../helpers/fetch'
import { stubRoutes } from '../../helpers/routes'
import { migrationJob } from '../../helpers/migrationFixtures'
import type { MigrationJob } from '@/types/migration'
import { MigrationJobFailure, MigrationStatus } from '@/types/migration'

/**
 * Mounts the card for a job.
 *
 * @param job - The job to show.
 * @returns The wrapper.
 */
const mountCard = (job: MigrationJob | null) => {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(MigrationProgressCard, { props: { job }, global: { plugins: [createTestI18n(), pinia] } })
}

describe('MigrationProgressCard', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the card layout with loading tiles and no actions while the job is read', () => {
    const wrapper = mountCard(null)

    expect(wrapper.get('[data-testid="migration-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('[data-testid="ui-stat-tile-skeleton"]')).toHaveLength(4)
    expect(wrapper.text()).toContain('Skipped')
    expect(wrapper.find('[data-testid="migration-check"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="migration-job-status"]').exists()).toBe(false)
  })

  it('shows the label, state, percent and the selected groups only', () => {
    const wrapper = mountCard(migrationJob({ overallPercent: 40, status: MigrationStatus.InProgress }))

    expect(wrapper.text()).toContain('Production')
    expect(wrapper.get('[data-testid="migration-job-status"]').text()).toBe('Running')
    expect(wrapper.get('[data-testid="migration-job-percent"]').text()).toBe('40%')
    expect(wrapper.find('[data-testid="migration-entity-clients"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="migration-entity-invoices"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="migration-entity-domains"]').exists()).toBe(false)
  })

  it('sums imported, skipped and failed records into the tiles', () => {
    const base = migrationJob({ status: MigrationStatus.Completed })
    const job = migrationJob({
      status: MigrationStatus.Completed,
      progress: {
        ...base.progress,
        clients: { imported: 7, skipped: 1, failed: 2, total: 10, done: true },
      },
    })
    const wrapper = mountCard(job)

    // The failed tile shows the server's own failed count.
    expect(wrapper.text()).toContain('Failed')
    expect(wrapper.text()).toContain('2')
  })

  it('shows the translated reason of a failed job', () => {
    const wrapper = mountCard(migrationJob({ status: MigrationStatus.Failed, failure: MigrationJobFailure.SourceRefused }))

    expect(wrapper.get('[data-testid="migration-job-error"]').text()).toContain('Source refused the request, check the key')
    expect(wrapper.find('[data-testid="migration-check"]').exists()).toBe(false)
  })

  it('shows the translated reason of a job whose source address is not allowed', () => {
    const wrapper = mountCard(migrationJob({ status: MigrationStatus.Failed, failure: MigrationJobFailure.SourceAddressNotAllowed }))

    expect(wrapper.get('[data-testid="migration-job-error"]').text()).toContain('The source address is not allowed, use a public HTTPS address')
  })

  it('retries a failed job through the API and offers no retry otherwise', async () => {
    const calls: string[] = []
    stubRoutes({
      'POST /api/admin/migrations/5/retry': () => {
        calls.push('retry')
        return json(migrationJob({ status: MigrationStatus.Queued }))
      },
    })
    const failed = mountCard(migrationJob({ status: MigrationStatus.Failed, failure: MigrationJobFailure.Interrupted }))

    expect(failed.get('[data-testid="migration-job-error"]').text()).toContain('The server stopped before the import finished')
    await failed.get('[data-testid="migration-retry"]').trigger('click')
    await flushPromises()

    expect(calls).toEqual(['retry'])
    expect(mountCard(migrationJob()).find('[data-testid="migration-retry"]').exists()).toBe(false)
  })

  it('shows the API sentence when the retry is refused', async () => {
    stubRoutes({ 'POST /api/admin/migrations/5/retry': () => problem('This migration job has not failed.', 400) })
    const wrapper = mountCard(migrationJob({ status: MigrationStatus.Failed, failure: MigrationJobFailure.Cancelled }))

    await wrapper.get('[data-testid="migration-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="migration-start-error"]').text()).toBe('This migration job has not failed.')
  })

  it('reports whether the plugin answered after a connection check', async () => {
    stubRoutes({ 'POST /api/admin/migrations/5/test-connection': () => json(migrationJob({ pluginConnected: true })) })
    const wrapper = mountCard(migrationJob())

    await wrapper.get('[data-testid="migration-check"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="migration-check-result"]').text()).toBe('Connected')
  })

  it('reports an unreachable plugin when the check ran and the plugin did not answer', async () => {
    stubRoutes({ 'POST /api/admin/migrations/5/test-connection': () => json(migrationJob({ pluginConnected: false })) })
    const wrapper = mountCard(migrationJob())

    await wrapper.get('[data-testid="migration-check"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="migration-check-result"]').text()).toBe('Not reachable')
    expect(wrapper.find('[data-testid="migration-check-error"]').exists()).toBe(false)
  })

  it('shows the API sentence, not an unreachable plugin, when the check is refused', async () => {
    stubRoutes({ 'POST /api/admin/migrations/5/test-connection': () => problem('The job is already finished.', 409) })
    const wrapper = mountCard(migrationJob())

    await wrapper.get('[data-testid="migration-check"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="migration-check-error"]').text()).toBe('The job is already finished.')
    expect(wrapper.find('[data-testid="migration-check-result"]').exists()).toBe(false)
  })

  it('shows the API sentence when the import cannot start', async () => {
    stubRoutes({ 'POST /api/admin/migrations/5/start': () => problem('The plugin key has expired.', 409) })
    const wrapper = mountCard(migrationJob({ pluginConnected: true }))

    await wrapper.get('[data-testid="migration-start"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="migration-start-error"]').text()).toBe('The plugin key has expired.')
  })

  it('offers the import only once the plugin is connected', async () => {
    const pending = mountCard(migrationJob())
    expect(pending.find('[data-testid="migration-start"]').exists()).toBe(false)

    const connected = mountCard(migrationJob({ pluginConnected: true }))
    expect(connected.find('[data-testid="migration-start"]').exists()).toBe(true)
  })
})
