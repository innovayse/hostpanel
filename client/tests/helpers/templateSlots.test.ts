/**
 * Tests for the template slot stand-in: a registry loader resolves to the component given for its slot, in every
 * template, and the slots asked for are recorded.
 *
 * @module tests/helpers/templateSlots.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { templates } from '~/templates/registry'
import { stubTemplateSlots } from '~/tests/helpers/templateSlots'
import { TemplateName } from '~/types/templatename'
import { TemplateSlot } from '~/types/templateslot'

/** The component the test gives for the checkout slot. */
const CheckoutStandIn = defineComponent({ name: 'CheckoutStandIn', setup: () => () => h('div') })

/**
 * Asks for a slot the way `useTemplate` does, through the `defineAsyncComponent` global.
 *
 * @param loader - A loader of the template registry.
 * @returns What the global resolves it to.
 */
const resolve = (loader: unknown): unknown =>
  (globalThis as unknown as { defineAsyncComponent: (loader: unknown) => unknown }).defineAsyncComponent(loader)

describe('stubTemplateSlots', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('has no two slots, in any template, that share a loader', () => {
    const loaders = Object.values(templates).flatMap((slots) => Object.values(TemplateSlot).map((slot) => slots[slot]))

    expect(new Set(loaders).size).toBe(loaders.length)
  })

  it('resolves the loader of a slot in every template to the stand-in and records the request', () => {
    const requested = stubTemplateSlots({ [TemplateSlot.Checkout]: CheckoutStandIn })

    for (const name of Object.values(TemplateName)) {
      expect(resolve(templates[name][TemplateSlot.Checkout])).toBe(CheckoutStandIn)
    }

    expect(requested).toEqual([TemplateSlot.Checkout, TemplateSlot.Checkout, TemplateSlot.Checkout])
  })

  it('leaves a slot without a stand-in to the real loader', () => {
    const requested = stubTemplateSlots({ [TemplateSlot.Checkout]: CheckoutStandIn })

    expect(resolve(templates[TemplateName.Classic][TemplateSlot.Home])).not.toBe(CheckoutStandIn)
    expect(requested).toEqual([])
  })
})
