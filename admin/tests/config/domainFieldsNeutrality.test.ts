/**
 * The admin holds nothing specific to a zone or a country in what it does with the extra domain fields: which fields a registry
 * asks for comes from the registrar plugin's manifest through `GET /api/domains/fields`, and a test that needs a zone invents
 * one. The rule is proven to bite on planted in-memory sources.
 */

import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { hitsIn, offenders, readFeatureSources } from '../helpers/domainFieldsNeutrality'

/** The admin root. */
const ADMIN_ROOT = resolve(import.meta.dirname, '..', '..')

/** The authored folders the feature lives in, tests included. */
const SCANNED = ['src', 'tests'] as const

/** Every authored file of the feature. */
const sources = readFeatureSources(ADMIN_ROOT, SCANNED)

describe('zone neutrality of the extra domain fields in the admin', () => {
  it('finds the feature files, so the rule cannot pass over nothing', () => {
    const names = sources.map(source => source.name)

    expect(names).toContain('src/components/clients/DomainFieldsForm.vue')
    expect(names).toContain('src/lib/domainFields.ts')
  })

  it('names no zone, no country and no national document', () => {
    expect(offenders(sources)).toEqual([])
  })

  it('fails on planted zones and documents', () => {
    const planted = [
      { name: 'planted/bare.ts', text: "const zone = '" + 'x' + 'x' + "'" },
      { name: 'planted/dotted.ts', text: "const name = 'example." + 'x' + 'x' + "'" },
      { name: 'planted/generic.ts', text: 'const name = "example.' + 'com' + '"' },
      { name: 'planted/document.vue', text: '<!-- asks for the ' + 'Pass' + 'port number -->' },
      { name: 'planted/clean.ts', text: "const name = 'name.alpha'; const key = 'holder-number'" },
    ]

    expect(offenders(planted).map(line => line.split(' -> ')[0])).toEqual([
      'planted/bare.ts',
      'planted/dotted.ts',
      'planted/generic.ts',
      'planted/document.vue',
    ])
  })

  it('ignores an invented zone and plain keys', () => {
    expect(hitsIn("const name = 'name.alpha'; const key = 'holder-number'; const path = '/api/domains/fields?domain=a.b'")).toEqual([])
  })
})
