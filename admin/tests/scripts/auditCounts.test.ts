/**
 * Tests for the counting helpers behind `npm run audit`: the audit must count code, not the
 * comments that explain it, and must read a whole opening tag even when a binding contains `>`.
 */
import { describe, expect, it } from 'vitest'
// @ts-expect-error The helpers are plain ES modules without type declarations.
import { countAny, countInputsWithoutPlaceholder, countStoresCallingUseApi, countVHtml, stripComments } from '../../scripts/auditCounts.mjs'

describe('stripComments', () => {
  it('removes block, line and HTML comments but keeps a URL inside a string', () => {
    const text = "/** doc */\nconst a = 'http://x.test' // trailing\n<!-- note -->"

    expect(stripComments(text)).toContain("'http://x.test'")
    expect(stripComments(text)).not.toMatch(/doc|trailing|note/)
  })
})

describe('countVHtml', () => {
  it('ignores a comment that mentions v-html and counts a real directive', () => {
    expect(countVHtml(['<script>\n/** Drawn without `v-html`. */\n</script>'])).toBe(0)
    expect(countVHtml(['<template><div v-html="raw" /></template>'])).toBe(1)
  })
})

describe('countAny', () => {
  it('ignores prose such as "choices: any" in a comment and counts the type', () => {
    expect(countAny(['/** Rating choices: any, then every second score. */\nconst a = 1'])).toBe(0)
    expect(countAny(['const a: any = 1', 'const b = c as any'])).toBe(2)
  })
})

describe('countInputsWithoutPlaceholder', () => {
  it('finds the placeholder after a binding that contains a greater-than sign', () => {
    const vue = '<template><UiSelect v-if="items.length > 1" :placeholder="t(\'x\')" /></template>'

    expect(countInputsWithoutPlaceholder(vue)).toBe(0)
  })

  it('counts an input that has none and skips checkboxes', () => {
    const vue = '<template><UiInput v-model="a" /><UiInput type="checkbox" v-model="b" /></template>'

    expect(countInputsWithoutPlaceholder(vue)).toBe(1)
  })
})

describe('countStoresCallingUseApi', () => {
  it('ignores a store that only names useApi in a comment and counts one that calls it', () => {
    const commentOnly = '/** Not a credential for {@link useApi}. */\nconst a = 1'
    const calling = "import { useApi } from '@/composables/useApi'\nconst api = useApi()"

    expect(countStoresCallingUseApi([commentOnly])).toBe(0)
    expect(countStoresCallingUseApi([commentOnly, calling])).toBe(1)
  })
})
