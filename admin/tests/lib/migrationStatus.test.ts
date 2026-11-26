/**
 * Unit tests for the migration presentation helpers: every job state and log outcome has a tone
 * and a caption key, every skip reason and job failure has a translated caption, and only Pending and InProgress jobs count as active.
 */
import { describe, expect, it } from 'vitest'
import { isJobActive, isSameJob, migrationActionLabelKey, migrationJobFailureLabelKey, migrationSkipReasonLabelKey, migrationStatusLabelKey } from '@/lib/migrationStatus'
import { migrationActionTone, migrationStatusTone } from '@/lib/tones'
import { MigrationJobFailure, MigrationLogAction, MigrationSkipReason, MigrationStatus } from '@/types/migration'
import { createTestI18n } from '../helpers/i18n'

describe('migrationStatus', () => {
  it('gives every job state a tone and a caption key', () => {
    for (const status of Object.values(MigrationStatus)) {
      expect(migrationStatusTone[status]).toBeTruthy()
      expect(migrationStatusLabelKey[status]).toMatch(/^migration\.status\./)
    }
  })

  it('gives every log outcome a tone and a caption key', () => {
    for (const action of Object.values(MigrationLogAction)) {
      expect(migrationActionTone[action]).toBeTruthy()
      expect(migrationActionLabelKey[action]).toMatch(/^migration\./)
    }
  })

  it('gives every skip reason a caption key that the catalogue defines', () => {
    for (const reason of Object.values(MigrationSkipReason)) {
      const key = migrationSkipReasonLabelKey[reason]
      expect(key).toMatch(/^migration\.skipReason\./)
      expect(createTestI18n().global.te(key)).toBe(true)
    }
  })

  it('gives every job failure a caption key that the catalogue defines', () => {
    for (const failure of Object.values(MigrationJobFailure)) {
      const key = migrationJobFailureLabelKey[failure]
      expect(key).toMatch(/^migration\.jobFailure\./)
      expect(createTestI18n().global.te(key)).toBe(true)
    }
  })

  it('colours failure as danger and success as success', () => {
    expect(migrationStatusTone[MigrationStatus.Failed]).toBe('danger')
    expect(migrationStatusTone[MigrationStatus.Completed]).toBe('success')
    expect(migrationActionTone[MigrationLogAction.Skipped]).toBe('warning')
  })

  it('treats only pending and running jobs as active', () => {
    expect(isJobActive(MigrationStatus.Pending)).toBe(true)
    expect(isJobActive(MigrationStatus.InProgress)).toBe(true)
    expect(isJobActive(MigrationStatus.Completed)).toBe(false)
    expect(isJobActive(MigrationStatus.Failed)).toBe(false)
  })

  it('tells a copy that reads the same from one that changed, nested counters included', () => {
    const held = { id: 1, status: MigrationStatus.InProgress, progress: { clients: { imported: 1 } } } as never
    expect(isSameJob(held, { id: 1, status: MigrationStatus.InProgress, progress: { clients: { imported: 1 } } } as never)).toBe(true)
    expect(isSameJob(held, { id: 1, status: MigrationStatus.InProgress, progress: { clients: { imported: 2 } } } as never)).toBe(false)
  })
})
