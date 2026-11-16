/**
 * Unit tests for WhyChooseUs: a heading over a list of capability cards, with gated cards hidden
 * until the operator has filled their setting in.
 *
 * @module tests/templates/nova/sections/WhyChooseUs.test
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import WhyChooseUs from '~/templates/nova/sections/WhyChooseUs.vue'
import { WHY_FEATURES } from '~/templates/nova/content'
import type { Feature } from '~/templates/nova/types/feature'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'

/** The stub server and the loader of the template globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

/**
 * Counts the features that do not wait on an operator setting.
 *
 * @param features A content list.
 * @returns How many are always shown.
 */
const ungated = (features: readonly Feature[]): number => features.filter(feature => !feature.requiresSetting).length

describe('WhyChooseUs', () => {
  it('lists every ungated card under an h2', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(WhyChooseUs)

    expect(wrapper.find('h2').text()).toBe('nova.why.title')
    expect(wrapper.findAll('ul > li')).toHaveLength(ungated(WHY_FEATURES))
  })
})
