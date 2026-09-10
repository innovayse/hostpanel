/**
 * The admin panel carries a copy of `brandPalette.ts` because it cannot import across the
 * repository; this test is what keeps the copy honest.
 *
 * @module utils/brandPalette.shared.test
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const here = (rel: string): string => fileURLToPath(new URL(rel, import.meta.url))

describe('admin copy of brandPalette.ts', () => {
  it('is identical to the client file below its header', () => {
    const client = readFileSync(here('./brandPalette.ts'), 'utf8')
    const admin = readFileSync(here('../../admin/src/utils/brandPalette.ts'), 'utf8')
    // The admin copy starts with one doc comment explaining why it exists; everything after
    // that comment must match byte for byte.
    const body = admin.slice(admin.indexOf('*/') + 3)
    expect(body).toBe(client)
  })
})
