/**
 * Unit tests for the plugin change readers: a failed last change becomes a failure with its sentence, and an applied,
 * missing or null one is no failure.
 */
import { describe, expect, it } from 'vitest'
import { failedChangeOf, failureSentence } from '@/lib/pluginChange'
import { PluginChangeResult } from '@/types/plugin'
import type { PluginLastChangeDto, PluginListItemDto } from '@/types/plugin'

/**
 * Builds a listed plugin carrying a last change.
 *
 * @param change - The last change, or undefined for a row that does not carry the field.
 * @returns The list row, with only the field this reader looks at.
 */
const withChange = (change: PluginLastChangeDto | null | undefined): PluginListItemDto => ({ lastChange: change }) as PluginListItemDto

/** A change that failed and restored the previous version. */
const failed: PluginLastChangeDto = {
  pluginId: 'example-pay',
  fromVersion: '1.0.0',
  toVersion: '2.0.0',
  result: PluginChangeResult.Failed,
  error: 'Missing dependency.',
  restoredVersion: '1.0.0',
  at: '2026-11-24T10:00:00Z',
}

describe('failedChangeOf', () => {
  it('reads the reason and the restored version of a failed change', () => {
    expect(failedChangeOf(withChange(failed))).toEqual({ error: 'Missing dependency.', restoredVersion: '1.0.0' })
  })

  it('is null for an applied change, a null change and a row without the field', () => {
    expect(failedChangeOf(withChange({ ...failed, result: PluginChangeResult.Applied }))).toBeNull()
    expect(failedChangeOf(withChange(null))).toBeNull()
    expect(failedChangeOf(withChange(undefined))).toBeNull()
  })
})

describe('failureSentence', () => {
  it('names the reason and the version still running', () => {
    expect(failureSentence({ error: 'Missing dependency.', restoredVersion: '1.0.0' })).toBe('Update failed: Missing dependency. Version 1.0.0 is still running.')
  })

  it('leaves out the version when none was restored, and says so when no reason was given', () => {
    expect(failureSentence({ error: null, restoredVersion: null })).toBe('Update failed: The new version did not load.')
  })
})
