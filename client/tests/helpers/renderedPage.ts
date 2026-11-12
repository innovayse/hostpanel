/**
 * Mounts a page whose data crosses a real socket and waits until the page itself is on screen.
 *
 * A Nuxt page awaits its reads at the top of its setup, so under `Suspense` it renders only once the
 * stub BFF has answered. `flushPromises` alone returns before that, so the mount waits for an element
 * the page always renders instead of for a number of ticks. One waiting rule serves the public pages
 * (mounted through `publicPage.ts`) and the client-area pages (mounted through `mountClient.ts`).
 *
 * @module tests/helpers/renderedPage
 */

import type { VueWrapper } from '@vue/test-utils'
import { expect } from 'vitest'
import type { Component } from 'vue'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { mountPage as mountClientPage } from '~/tests/helpers/mountClient'
import { mountPage as mountPublicPage } from '~/tests/helpers/publicPage'
import { waitUntil } from '~/tests/helpers/waitUntil'

/**
 * Mounts a page with the given mounter and waits until the element with the given test id has rendered.
 *
 * @param mount - The page mounter of the area the page belongs to.
 * @param page - The page component.
 * @param markerTestId - The `data-testid` of an element the page always renders once its setup has finished.
 * @param options - Props, attributes, stubs and the error handler, as for the mounter.
 * @returns The wrapper, once the marker is on screen.
 * @throws When the marker never appears (for instance because the page's setup threw): the caller waits on its error handler instead.
 */
const mountUntilRendered = async <Options>(
  mount: (page: Component, options: Options) => Promise<VueWrapper>,
  page: Component,
  markerTestId: string,
  options: Options
): Promise<VueWrapper> => {
  const wrapper = await mount(page, options)
  await waitUntil(() => expect(wrapper.find(`[data-testid="${markerTestId}"]`).exists()).toBe(true))
  return wrapper
}

/**
 * Mounts a public page and waits until its root element has rendered.
 *
 * @param page - The page component.
 * @param rootTestId - The `data-testid` of the page's root element.
 * @param options - Props, attributes, stubs and the error handler, as for `mountPage` in `publicPage.ts`.
 * @returns The wrapper, once the page has rendered.
 */
export const mountRenderedPage = (page: Component, rootTestId: string, options: MountWithKitOptions = {}): Promise<VueWrapper> =>
  mountUntilRendered(mountPublicPage, page, rootTestId, options)

/**
 * Mounts a client-area page and waits until its marker element has rendered. The kit and the client
 * components are registered as for `mountPage` in `mountClient.ts`.
 *
 * @param page - The page component.
 * @param markerTestId - The `data-testid` of an element the page always renders once its setup has finished.
 * @param options - Extra global components or stubs, and optionally a Pinia the test filled, as for `mountPage`.
 * @returns The wrapper, once the marker is on screen.
 */
export const mountRenderedClientPage = (
  page: Component,
  markerTestId: string,
  options: Parameters<typeof mountClientPage>[1] = {}
): Promise<VueWrapper> => mountUntilRendered(mountClientPage, page, markerTestId, options)
