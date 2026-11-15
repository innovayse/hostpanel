/**
 * The entrance reveal must not hide content in the markup: a block that carries an inline
 * `opacity: 0` is invisible in server HTML until a script reveals it, and stays so if that
 * script never fires.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { stubLocalePath, TEST_LOCALE_PREFIX } from '~/tests/helpers/runtimeStubs'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import ProductsBenefits from '~/components/catalog/ProductsBenefits.vue'
import MarketingSectionHeader from '~/components/marketing/MarketingSectionHeader.vue'
import SectionsCTA from '~/components/sections/SectionsCTA.vue'
import UiPageHero from '~/components/ui/UiPageHero.vue'

/** Blocks that reveal on entrance, each mounted the way their own tests mount them. */
const REVEALING = {
  ProductsBenefits: () => mountWithKit(ProductsBenefits, { components: { MarketingSectionHeader }, stubs: { 'i18n-t': true } }),
  MarketingSectionHeader: () => mountWithKit(MarketingSectionHeader),
  SectionsCTA: () => mountWithKit(SectionsCTA),
}

/** Blocks that must stay fully opaque while they enter: the page header only slides. */
const SLIDING = {
  UiPageHero: () => mountWithKit(UiPageHero, { props: { title: 'Knowledgebase' } }),
}

describe('entrance reveal markup', () => {
  beforeEach(() => {
    stubLocalePath(TEST_LOCALE_PREFIX)
  })

  for (const [name, mount] of Object.entries(REVEALING)) {
    it(`${name} renders no inline opacity:0 and reveals with the CSS animation`, () => {
      const html = mount().html()

      expect(html).not.toMatch(/opacity:\s*0\b/)
      expect(html).toContain('animate-reveal-up')
      expect(html).toContain('motion-reduce:animate-none')
    })
  }

  for (const [name, mount] of Object.entries(SLIDING)) {
    it(`${name} renders no inline opacity:0 and slides in without ever fading from transparent`, () => {
      const html = mount().html()

      expect(html).not.toMatch(/opacity:\s*0(?!\d)/)
      expect(html).not.toContain('animate-reveal-up')
      expect(html).toContain('animate-slide-up')
      expect(html).toContain('motion-reduce:animate-none')
    })
  }
})
