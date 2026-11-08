/**
 * Tests for the `Permission` flags: every single flag combined is the `All` value.
 *
 * @module tests/types/permission.test
 */

import { describe, expect, it } from 'vitest'
import { Permission } from '~/types/permission'

describe('Permission', () => {
  it('has an All value that is every single flag combined', () => {
    const flags = Object.values(Permission).filter((value): value is number => typeof value === 'number' && value !== Permission.All)

    expect(flags.reduce((all, flag) => all | flag, 0)).toBe(Permission.All)
  })
})
