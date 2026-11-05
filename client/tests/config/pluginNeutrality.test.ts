/**
 * The client portal is plugin neutral: no source names a plugin id or a plugin entry point type, so the
 * portal reaches a plugin only through its manifest and the host's plugin endpoints. The rule is proven to
 * bite on a planted in-memory source, never a committed bad file.
 *
 * @module tests/config/pluginNeutrality.test
 */

import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { namesIn, offenders, PLUGIN_NAMES, readSources } from '../helpers/pluginNeutrality'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The authored folders and files of the client portal; tests, dependencies and build output are not scanned. */
const SCANNED = [
  'app.vue',
  'error.vue',
  'components',
  'composables',
  'constants',
  'layouts',
  'lib',
  'locales',
  'middleware',
  'pages',
  'plugins',
  'server',
  'stores',
  'templates',
  'types',
  'utils',
] as const

/**
 * Files that still name a built-in provider. Empty: the portal names no plugin. An entry whose file no
 * longer names a plugin fails the stale-entry check below, so the list can only shrink.
 */
const LEGACY_BUILT_IN: readonly string[] = []

/** Every authored source of the client portal. */
const sources = readSources(CLIENT_ROOT, SCANNED)

describe('plugin neutrality of the client portal', () => {
  it('names no plugin outside the legacy built-in list', () => {
    expect(offenders(sources.filter((source) => !LEGACY_BUILT_IN.includes(source.name)))).toEqual([])
  })

  it('keeps no stale entry in the legacy list', () => {
    LEGACY_BUILT_IN.forEach((name) => {
      const source = sources.find((candidate) => candidate.name === name)

      expect(source, `${name} must be an existing source`).toBeDefined()
      expect(namesIn(source?.text ?? ''), `${name} no longer names a plugin, remove it from the list`).not.toEqual([])
    })
  })

  it('fails on a planted plugin id and a planted plugin type name', () => {
    const id = PLUGIN_NAMES[0]
    const type = PLUGIN_NAMES[3]
    const planted = [
      { name: 'components/plugins/Planted.vue', text: `<template><div v-if="id === '${id}'" /></template>` },
      { name: 'pages/planted.vue', text: `const kind = ${type}` },
      { name: 'pages/clean.vue', text: `const kind = 'other'` },
    ]

    expect(offenders(planted)).toEqual([`components/plugins/Planted.vue -> ${id}`, `pages/planted.vue -> ${type}`])
  })

  it('does not count a name inside a longer token', () => {
    const name = PLUGIN_NAMES[2]

    expect(namesIn(`const a = ${name}Client; const b = '${name}-more'; const c = Prefix${name}`)).toEqual([])
  })
})
