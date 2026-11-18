/**
 * Stands in for the template components a page renders, so a page test reads what the page hands its template without
 * mounting the template's own markup.
 *
 * `useTemplate` is the real composable: it names the template from the operator's setting and resolves a slot through
 * `defineAsyncComponent`, a Vue auto-import. This helper replaces only that Vue global: a loader of the template
 * registry resolves to the component the test gave for its slot, and anything else still loads for real.
 *
 * @module tests/helpers/templateSlots
 */

import { defineAsyncComponent } from 'vue'
import type { Component } from 'vue'
import { vi } from 'vitest'
import { templates } from '~/templates/registry'
import type { TemplateLoader } from '~/templates/registry'
import { TemplateSlot } from '~/types/templateslot'

/** The slot every loader of the registry fills, whichever template it belongs to. */
const SLOT_OF_LOADER: ReadonlyMap<TemplateLoader, TemplateSlot> = new Map(
  Object.values(templates).flatMap((loaders) => Object.values(TemplateSlot).map((slot): [TemplateLoader, TemplateSlot] => [loaders[slot], slot]))
)

/**
 * Makes the slots named in `components` resolve to the given stand-in components.
 *
 * @param components - The stand-in component of each slot the test renders.
 * @returns The slots the page asked for, in the order it asked, filled while the page mounts.
 */
export const stubTemplateSlots = (components: Partial<Record<TemplateSlot, Component>>): TemplateSlot[] => {
  const requested: TemplateSlot[] = []
  vi.stubGlobal('defineAsyncComponent', (loader: TemplateLoader) => {
    const slot = SLOT_OF_LOADER.get(loader)
    const standIn = slot === undefined ? undefined : components[slot]
    if (slot === undefined || standIn === undefined) {
      return defineAsyncComponent(loader as () => Promise<Component>)
    }
    requested.push(slot)
    return standIn
  })
  return requested
}
