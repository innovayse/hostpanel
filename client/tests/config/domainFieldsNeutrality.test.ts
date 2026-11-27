/**
 * The client portal holds nothing specific to a zone or a country in what it does with the extra domain fields: which
 * fields a registry asks for comes from the registrar plugin's manifest through `GET /api/portal/public/domain-fields`,
 * and a test that needs a zone invents one. The rule is proven to bite on planted in-memory sources.
 *
 * @module tests/config/domainFieldsNeutrality.test
 */

import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { hitsIn, offenders, readFeatureSources } from '../helpers/domainFieldsNeutrality'

/** The client root. */
const CLIENT_ROOT = resolve(import.meta.dirname, '..', '..')

/** The authored folders the feature lives in, tests included. */
const SCANNED = ['components', 'composables', 'server', 'stores', 'types', 'utils', 'tests'] as const

/** Every authored file of the feature. */
const sources = readFeatureSources(CLIENT_ROOT, SCANNED)

describe('zone neutrality of the extra domain fields in the client portal', () => {
  it('finds the feature files, so the rule cannot pass over nothing', () => {
    const names = sources.map(source => source.name)

    expect(names).toContain('components/domains/DomainFieldsForm.vue')
    expect(names).toContain('utils/domainFields.ts')
    expect(names).toContain('components/checkout/CheckoutDomainFields.vue')
  })

  it('names no zone, no country and no national document', () => {
    expect(offenders(sources)).toEqual([])
  })

  it('fails on planted zones and documents', () => {
    const planted = [
      { name: 'planted/bare.ts', text: `const zone = '${'x'}${'x'}'` },
      { name: 'planted/dotted.ts', text: `const name = 'example.${'x'}${'x'}'` },
      { name: 'planted/generic.ts', text: `const name = "example.${'com'}"` },
      { name: 'planted/document.vue', text: `<!-- asks for the ${'Pass'}port number -->` },
      { name: 'planted/clean.ts', text: `const name = 'name.alpha'; const key = 'holder-number'` },
    ]

    expect(offenders(planted).map(line => line.split(' -> ')[0])).toEqual([
      'planted/bare.ts',
      'planted/dotted.ts',
      'planted/generic.ts',
      'planted/document.vue',
    ])
  })

  it('ignores an invented zone and plain keys', () => {
    expect(hitsIn(`const name = 'name.alpha'; const key = 'holder-number'; const path = '/api/domains/fields?domain=a.b'`)).toEqual([])
  })
})
