/**
 * Mount helper for the account components: the kit plus every `components/account/*.vue`.
 *
 * The account components render one another (`AccountProfileTab` renders `AccountProfileForm`,
 * and so on) under the names Nuxt auto-imports them by: the `Account` directory prefix plus the
 * file name. Registering them here keeps a test mounting the same tree the page renders.
 *
 * @module tests/helpers/mountAccount
 */

import type { VueWrapper } from '@vue/test-utils'
import type { Component } from 'vue'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'

/** Every account component file, loaded once. */
const ACCOUNT_MODULES = import.meta.glob<{ default: Component }>('../../components/account/*.vue', { eager: true })

/**
 * Registers the account components under their auto-import names.
 *
 * @returns Map of component name to component.
 */
const buildAccountComponents = (): Record<string, Component> => {
  const registry: Record<string, Component> = {}
  for (const [path, module] of Object.entries(ACCOUNT_MODULES)) {
    const base = path.split('/').pop()!.replace(/\.vue$/, '') // glob keys always end in a file name
    registry[`Account${base}`] = module.default
  }
  return registry
}

/** The account components, built once for every mount; a page that renders the tabs registers them too. */
export const ACCOUNT_COMPONENTS = buildAccountComponents()

/**
 * Mounts an account component with the kit and its sibling components registered.
 *
 * @param component The component under test.
 * @param options Props, slots, attributes and stubs, as for {@link mountWithKit}.
 * @returns The Vue Test Utils wrapper.
 */
export const mountAccount = (component: Component, options: MountWithKitOptions = {}): VueWrapper =>
  mountWithKit(component, { ...options, components: { ...ACCOUNT_COMPONENTS, ...options.components } })
