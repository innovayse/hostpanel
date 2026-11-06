/**
 * The rendering rules of the pages that must not be server-rendered or indexed hold under every language prefix: a rule
 * matches the path, so a page missing here is server-rendered and indexable in Russian and Armenian alone.
 *
 * @module tests/config/localizedRouteRules.test
 */

import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

/** The configuration's source; its rules are literal, and importing it would need the Nuxt build context. */
const config = readFileSync(resolve(__dirname, '../../nuxt.config.ts'), 'utf8')

describe('browser-only page rules', () => {
  it.each(['client', 'cart', 'checkout', 'payment'].flatMap(area => ['', '/ru', '/hy'].map(prefix => `${prefix}/${area}/**`)))(
    '%s renders in the browser and is not indexed',
    (rule) => {
      expect(config).toContain(`'${rule}': { ssr: false, headers: { 'X-Robots-Tag': 'noindex, nofollow' } }`)
    },
  )
})
