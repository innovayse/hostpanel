/**
 * Unit tests for the text of the version button: the label of the build wins, so the button is the same
 * before and after the list of versions is read.
 */
import { describe, expect, it } from 'vitest'
import { switcherLabel } from '../.vitepress/theme/switcherLabel'
import { VersionKind } from '../.vitepress/theme/versionLinks'

describe('switcherLabel', () => {
  it('uses the label of the build, so the button is the same before the list is read', () => {
    expect(switcherLabel(VersionKind.Release, 'v0.3', undefined)).toBe('v0.3')
    expect(switcherLabel(VersionKind.Latest, 'v1.0', 'v1.0')).toBe('v1.0')
  })

  it('falls back to the label from the list', () => {
    expect(switcherLabel(VersionKind.Latest, '', 'v1.0')).toBe('v1.0')
  })

  it('reads latest for an unversioned build and stays empty for any other kind without a label', () => {
    expect(switcherLabel(VersionKind.Latest, '', undefined)).toBe('latest')
    expect(switcherLabel(VersionKind.Next, '', undefined)).toBe('')
  })
})
