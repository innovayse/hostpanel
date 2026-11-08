/**
 * Unit tests for versionOrder: numeric comparison of plugin versions and the relation of a listed
 * version to the installed one.
 */
import { describe, expect, it } from 'vitest'
import { compareVersions, relationTo } from '@/components/plugins/details/versionOrder'
import { VersionRelation } from '@/types/plugin'

describe('compareVersions', () => {
  it('compares segments as numbers, not text', () => {
    expect(compareVersions('1.10.0', '1.9.0')).toBeGreaterThan(0)
    expect(compareVersions('1.9.0', '1.10.0')).toBeLessThan(0)
  })

  it('treats a missing segment as zero', () => {
    expect(compareVersions('1.2', '1.2.0')).toBe(0)
  })

  it('ranks a release above its pre-release', () => {
    expect(compareVersions('2.0.0', '2.0.0-beta.1')).toBeGreaterThan(0)
    expect(compareVersions('2.0.0-beta.1', '2.0.0')).toBeLessThan(0)
  })

  it('orders two pre-releases by their numbers', () => {
    expect(compareVersions('2.0.0-beta.10', '2.0.0-beta.2')).toBeGreaterThan(0)
  })

  it('ignores build metadata', () => {
    expect(compareVersions('1.0.0+build5', '1.0.0')).toBe(0)
  })
})

describe('relationTo', () => {
  it('is fresh when nothing is installed', () => {
    expect(relationTo('1.0.0', null)).toBe(VersionRelation.Fresh)
  })

  it('names the installed, newer and older versions', () => {
    expect(relationTo('2.0.0', '2.0.0')).toBe(VersionRelation.Current)
    expect(relationTo('3.0.0', '2.0.0')).toBe(VersionRelation.Newer)
    expect(relationTo('1.0.0', '2.0.0')).toBe(VersionRelation.Older)
  })
})
