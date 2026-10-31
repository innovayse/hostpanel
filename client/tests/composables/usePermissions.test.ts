/**
 * Tests for `usePermissions`: the permission constants. Whether a person holds a flag is decided by
 * `mayPerform` (see tests/utils/holdsPermission.test.ts and the client store's `mayDo`).
 *
 * @module tests/composables/usePermissions.test
 */

import { describe, expect, it } from 'vitest'
import { Permission, usePermissions } from '~/composables/usePermissions'

describe('usePermissions', () => {
  it('exposes the permission constants', () => {
    expect(usePermissions().Permission).toBe(Permission)
  })

  it('has an All value that is every single flag combined', () => {
    const flags = Object.values(Permission).filter((value): value is number => typeof value === 'number' && value !== Permission.All)

    expect(flags.reduce((all, flag) => all | flag, 0)).toBe(Permission.All)
  })
})
