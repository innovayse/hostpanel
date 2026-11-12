/**
 * The admin panel carries a copy of the four plugin chat composables (`usePluginChatDraft`, `usePluginChatMessages`,
 * `usePluginChatScroll`, `usePluginChatTurn` in `admin/src/composables/`) because it cannot import across the repository; the client files are
 * the spec and this test is what keeps the copies honest. Only the import lines may differ, because Nuxt auto-imports
 * `ref`, `watch` and `onMounted` where the admin names them, and the two apps keep their types and link rule in their own
 * modules; every other byte must match.
 *
 * @module tests/composables/pluginChat.shared.test
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

/** The composables both apps carry. */
const SHARED = ['usePluginChatDraft', 'usePluginChatMessages', 'usePluginChatScroll', 'usePluginChatTurn'] as const

/**
 * Resolves a path relative to this test file.
 *
 * @param rel Relative path.
 * @returns The absolute file path.
 */
const here = (rel: string): string => fileURLToPath(new URL(rel, import.meta.url))

/**
 * Reads a composable without its import lines (single-line imports, the only kind these files use).
 *
 * @param rel Relative path of the file.
 * @returns The source below its imports.
 */
const withoutImports = (rel: string): string => readFileSync(here(rel), 'utf8').replace(/^import .*\r?\n/gm, '')

describe('admin copies of the plugin chat composables', () => {
  it.each(SHARED)('%s is identical to the client file apart from its imports', (name) => {
    const client = withoutImports(`../../composables/${name}.ts`)
    const admin = withoutImports(`../../../admin/src/composables/${name}.ts`)

    // Both files must really be read: a moved or renamed file would otherwise compare two empty strings.
    expect(client).toContain(`export const ${name}`)
    expect(admin).toContain(`export const ${name}`)
    expect(admin).toBe(client)
  })

  it.each(SHARED)('%s keeps its imports on single lines, so the comparison sees all of them', (name) => {
    for (const rel of [`../../composables/${name}.ts`, `../../../admin/src/composables/${name}.ts`]) {
      expect(readFileSync(here(rel), 'utf8')).not.toMatch(/^import [^'\n]*$/m)
    }
  })
})
