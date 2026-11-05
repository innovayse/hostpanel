/**
 * The admin interface is plugin neutral: no source under `src` names a plugin id or a plugin entry point
 * type, so the panel reaches a plugin only through its manifest and the host's plugin endpoints. The
 * slot and plugin page components are held to the rule without an exemption. The rule is proven to bite on a
 * planted in-memory source, never a committed bad file.
 */
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { namesIn, offenders, PLUGIN_NAMES, readSources } from '../helpers/pluginNeutrality'

/** The admin root: the folder above `tests/`. */
const ROOT = join(__dirname, '..', '..')

/**
 * Files that still name a built-in provider. Empty: the panel names no plugin. An entry whose file no
 * longer names a plugin fails the stale-entry check below, so the list can only shrink.
 */
const LEGACY_BUILT_IN: readonly string[] = []

/** Every authored source of the admin interface. */
const sources = readSources(ROOT, ['src'])

describe('plugin neutrality of the admin interface', () => {
  it('names no plugin outside the legacy built-in list', () => {
    expect(offenders(sources.filter((source) => !LEGACY_BUILT_IN.includes(source.name)))).toEqual([])
  })

  it('names no plugin in the slot and plugin page components', () => {
    const slotAndView = sources.filter((source) => source.name.startsWith('src/components/plugins/') || /Plugin\w*View\.vue$/.test(source.name))

    expect(slotAndView.length).toBeGreaterThan(0)
    expect(offenders(slotAndView)).toEqual([])
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
      { name: 'src/components/plugins/Planted.vue', text: `<template><UiCard v-if="id === '${id}'" /></template>` },
      { name: 'src/views/PlantedView.vue', text: `const kind = ${type}` },
      { name: 'src/views/CleanView.vue', text: `const kind = 'other'` },
    ]

    expect(offenders(planted)).toEqual([`src/components/plugins/Planted.vue -> ${id}`, `src/views/PlantedView.vue -> ${type}`])
  })

  it('does not count a name inside a longer token', () => {
    const name = PLUGIN_NAMES[2]

    expect(namesIn(`const a = ${name}Client; const b = '${name}-more'; const c = Prefix${name}`)).toEqual([])
  })
})
