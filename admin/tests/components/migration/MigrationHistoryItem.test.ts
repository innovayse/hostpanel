/**
 * Tests for MigrationHistoryItem: the name, number, state, progress and age of a job, the highlight of the open one, and
 * the two events (choose, delete).
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import MigrationHistoryItem from '@/components/migration/MigrationHistoryItem.vue'
import { MessageLanguage } from '@/locales/messages'
import { MigrationStatus } from '@/types/migration'
import { createTestI18n } from '../../helpers/i18n'
import { migrationJob } from '../../helpers/migrationFixtures'

/**
 * Mounts the item.
 *
 * @param props - Props to pass over the defaults (a pending job that is not open and not being deleted).
 * @returns The wrapper.
 */
const mountItem = (props: Record<string, unknown> = {}) =>
  mount(MigrationHistoryItem, {
    props: { job: migrationJob({ createdAt: new Date().toISOString() }), selected: false, deleting: false, ...props },
    global: { plugins: [createTestI18n()] },
  })

describe('MigrationHistoryItem', () => {
  it('shows the name, the job number, the state, the progress and how long ago it was created', () => {
    const wrapper = mountItem({ job: migrationJob({ status: MigrationStatus.InProgress, overallPercent: 40, createdAt: new Date().toISOString() }) })

    expect(wrapper.text()).toContain('Production')
    expect(wrapper.text()).toContain('Job #5')
    expect(wrapper.text()).toContain('Running')
    expect(wrapper.get('progress').attributes('value')).toBe('40')
    expect(wrapper.get('[data-testid="migration-job-created"]').text()).toBe('now')
  })

  it.each([
    [MigrationStatus.Completed, 'bg-status-green'],
    [MigrationStatus.Failed, 'bg-status-red'],
    [MigrationStatus.InProgress, 'bg-primary-500'],
  ])('colours the progress bar of a %s job by its state, never red for a finished one', (status, fill) => {
    const classes = mountItem({ job: migrationJob({ status, overallPercent: 100 }) }).get('progress').classes().join(' ')

    expect(classes).toContain(fill)
    expect(classes).not.toContain('gradient-bar-warn')
  })

  it.each([
    [MessageLanguage.Hy, '4 օր առաջ'],
    [MessageLanguage.En, '4 days ago'],
  ])('words how long ago the job was created in the %s locale', (locale, expected) => {
    const createdAt = new Date(Date.now() - 4 * 86_400_000).toISOString()
    const wrapper = mount(MigrationHistoryItem, {
      props: { job: migrationJob({ createdAt }), selected: false, deleting: false },
      global: { plugins: [createTestI18n(locale)] },
    })

    expect(wrapper.get('[data-testid="migration-job-created"]').text()).toBe(expected)
  })

  it('names a job without a label with the generic caption', () => {
    expect(mountItem({ job: migrationJob({ label: null, createdAt: new Date().toISOString() }) }).text()).toContain('Migration')
  })

  it('marks only the open job as selected', () => {
    expect(mountItem().attributes('data-selected')).toBeUndefined()
    expect(mountItem().get('[data-testid="migration-select-5"]').attributes('aria-pressed')).toBe('false')
    expect(mountItem({ selected: true }).attributes('data-selected')).toBe('true')
    expect(mountItem({ selected: true }).get('[data-testid="migration-select-5"]').attributes('aria-pressed')).toBe('true')
  })

  it('emits select with the job when it is chosen and delete with the job from the icon', async () => {
    const wrapper = mountItem()

    await wrapper.get('[data-testid="migration-select-5"]').trigger('click')
    await wrapper.get('[data-testid="migration-delete-5"]').trigger('click')

    expect(wrapper.emitted('select')?.[0]).toEqual([wrapper.props('job')])
    expect(wrapper.emitted('delete')?.[0]).toEqual([wrapper.props('job')])
  })

  it('blocks the delete icon while the job is being deleted', () => {
    expect(mountItem({ deleting: true }).get('[data-testid="migration-delete-5"]').attributes('disabled')).toBeDefined()
  })

  it('explains the delete icon with a hint', () => {
    expect(mountItem().get('[role="tooltip"]').text()).toBe('Delete migration')
  })
})
