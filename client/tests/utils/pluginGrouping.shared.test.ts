/**
 * The admin panel carries a copy of the plugin block grouping (`groupPluginBlocks` and the kinds it groups,
 * `admin/src/lib/pluginView.ts`) because it cannot import across the repository; the client file is the spec and
 * this test is what keeps the copy honest.
 *
 * @module tests/utils/pluginGrouping.shared.test
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/** The comment that opens the shared block in both files. */
const START = '// The block from here to the end of groupPluginBlocks is shared with the other app'

/**
 * Resolves a path relative to this test file.
 *
 * @param rel Relative path.
 * @returns The absolute file path.
 */
const here = (rel: string): string => fileURLToPath(new URL(rel, import.meta.url))

/**
 * Reads the shared block of a file: from its marker comment to the closing brace of `groupPluginBlocks`.
 *
 * @param rel Relative path of the file.
 * @returns The shared block; the call fails when a marker is missing.
 */
const sharedBlock = (rel: string): string => {
  const source = readFileSync(here(rel), 'utf8')
  const start = source.indexOf(START)
  const declaration = source.indexOf('export const groupPluginBlocks')
  const end = source.indexOf('\n}\n', declaration)
  // Slicing from -1 would compare the wrong text and could pass, so a missing marker fails here, loudly.
  expect(start, `${rel} has the start marker`).not.toBe(-1)
  expect(declaration, `${rel} declares groupPluginBlocks`).not.toBe(-1)
  expect(end, `${rel} closes groupPluginBlocks`).not.toBe(-1)
  expect(end, `${rel} closes groupPluginBlocks after the marker`).toBeGreaterThan(start)
  return source.slice(start, end + 3)
}

describe('admin copy of groupPluginBlocks', () => {
  it('is identical to the client block', () => {
    const client = sharedBlock('../../utils/plugins.ts')
    const admin = sharedBlock('../../../admin/src/lib/pluginView.ts')

    expect(client).toContain('export const groupPluginBlocks')
    expect(admin).toBe(client)
  })
})
