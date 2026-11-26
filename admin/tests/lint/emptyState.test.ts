/**
 * Guard: an empty table, card or section is drawn with the shared `UiEmptyState` (icon, text, standard padding), never
 * with a bare line of text. A table's `#empty` slot that holds a plain `UiText` sits against the header divider with
 * almost no padding; the kit's empty state brings the spacing, so a screen only passes it a title.
 *
 * The test reads every `.vue` file under `src` (the kit left out, it draws the default empty row itself), finds each
 * `<template #empty>` block and fails with the file when the block has no `<UiEmptyState`.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, sep } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The `src` folder of the admin. */
const SRC = join(__dirname, '..', '..', 'src')

/** The kit folder, which draws the default empty row. */
const KIT = join(SRC, 'components', 'ui')

/** An opening `#empty` slot template at the start of a line; the indent is group 1. */
const EMPTY_SLOT = /^([ \t]*)<template #empty>[ \t]*\r?\n/gm

/**
 * Lists every `.vue` file under a folder, the kit left out.
 *
 * @param dir - Absolute folder to walk.
 * @returns Absolute paths of the components found.
 */
const vueFiles = (dir: string): string[] =>
  readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) {
      return path === KIT ? [] : vueFiles(path)
    }
    return name.endsWith('.vue') ? [path] : []
  })

/**
 * Collects the body of every `#empty` slot of a component, up to the closing tag at the same indent.
 *
 * @param source - The `.vue` file contents.
 * @returns One string per slot found.
 */
const emptySlotBodies = (source: string): string[] =>
  [...source.matchAll(EMPTY_SLOT)].map((match) => {
    const start = (match.index ?? 0) + match[0].length
    const close = source.indexOf(`${match[1]}</template>`, start)
    return source.slice(start, close === -1 ? source.length : close)
  })

describe('empty states', () => {
  it('draws every #empty slot with UiEmptyState', () => {
    const offenders = vueFiles(SRC)
      .filter((path) => emptySlotBodies(readFileSync(path, 'utf8')).some((body) => !body.includes('<UiEmptyState')))
      .map((path) => relative(SRC, path).split(sep).join('/'))

    expect(offenders).toEqual([])
  })
})
